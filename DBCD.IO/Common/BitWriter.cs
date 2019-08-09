using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace DBCD.IO.Common
{
    class BitWriter : IEquatable<BitWriter>
    {
        private byte nAccumulatedBits;
        private byte[] buffer;

        private readonly byte[] _pool;

        public BitWriter(int capacity)
        {
            buffer = new byte[capacity];
            _pool = new byte[0x10];
        }

        public byte this[int i] => buffer[i];
        public int TotalBytesWrittenOut { get; private set; }


        public void WriteAligned<T>(T value) where T : struct
        {
            EnsureSize();
            Unsafe.WriteUnaligned(ref buffer[TotalBytesWrittenOut], value);
            TotalBytesWrittenOut += Unsafe.SizeOf<T>();
        }

        public void WriteCStringAligned(string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value);

            Resize(data.Length);
            Array.Copy(data, 0, buffer, TotalBytesWrittenOut, data.Length);
            TotalBytesWrittenOut += data.Length + 1;
        }

        public void Write<T>(T value, int nbits) where T : struct
        {
            if (nAccumulatedBits == 0 && (nbits & 7) == 0)
            {
                EnsureSize();
                Unsafe.WriteUnaligned(ref buffer[TotalBytesWrittenOut], value);
                TotalBytesWrittenOut += nbits / 8;
            }
            else
            {
                Unsafe.WriteUnaligned(ref _pool[0], value);
                for (int i = 0; nbits > 0; i++)
                {
                    WriteBits(Math.Min(nbits, 8), _pool[i]);
                    nbits -= 8;
                }
            }
        }

        public void Write<T>(T value, int nbits, int offset) where T : struct
        {
            Unsafe.WriteUnaligned(ref _pool[0], value);

            int byteOffset = offset >> 3;
            int lowLen = offset & 7;
            int highLen = 8 - lowLen;

            int i = 0;
            while ((nbits -= 8) >= 0)
            {
                // write last part of this byte
                buffer[byteOffset] = (byte)((buffer[byteOffset] & (0xFF >> highLen)) | (_pool[i] << lowLen));

                // write first part of next byte
                byteOffset++;
                buffer[byteOffset] = (byte)((buffer[byteOffset] & (0xFF << lowLen)) | (_pool[i] >> highLen));
                i++;
            }

            // write final bits
            if ((nbits &= 7) > 0)
            {
                lowLen = nbits;
                highLen = 8 - nbits;

                buffer[byteOffset] = (byte)((buffer[byteOffset] & (0xFF >> highLen)) | (_pool[i] << lowLen));
            }
        }

        public void WriteCString(string value)
        {
            // Note: cstrings are always aligned to 8 bytes
            if (nAccumulatedBits == 0)
            {
                WriteCStringAligned(value);
            }
            else
            {
                byte[] data = Encoding.UTF8.GetBytes(value);
                for (int i = 0; i < data.Length; i++)
                    WriteBits(8, data[i]);

                WriteBits(8, 0);
            }
        }


        private void WriteBits(int bitCount, uint value)
        {
            EnsureSize();

            for (int i = 0; i < bitCount; i++)
            {
                buffer[TotalBytesWrittenOut] |= (byte)(((value >> i) & 0x1) << nAccumulatedBits);
                nAccumulatedBits++;

                if (nAccumulatedBits > 7)
                {
                    TotalBytesWrittenOut++;
                    nAccumulatedBits = 0;
                }
            }
        }

        private void EnsureSize(int size = 8)
        {
            if (TotalBytesWrittenOut + size >= buffer.Length)
                Array.Resize(ref buffer, buffer.Length + size + 0x10);
        }


        public void Resize(int size)
        {
            if (TotalBytesWrittenOut < size)
            {
                EnsureSize(size - TotalBytesWrittenOut);
                TotalBytesWrittenOut = size;
            }
        }

        public void ResizeToMultiple(int divisor)
        {
            int remainder = TotalBytesWrittenOut % divisor;
            if (remainder != 0)
            {
                EnsureSize();
                TotalBytesWrittenOut += 4 - remainder;
            }
        }

        public void CopyTo(Stream stream)
        {
            stream.Write(buffer, 0, TotalBytesWrittenOut);
        }


        public bool Equals(BitWriter other)
        {
            if (TotalBytesWrittenOut != other.TotalBytesWrittenOut)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            for (int i = 0; i < TotalBytesWrittenOut; i++)
                if (this[i] != other[i])
                    return false;

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // jenkins one-at-a-time
                int hashcode = 0;
                for (int i = 0; i < TotalBytesWrittenOut; i++)
                {
                    hashcode += buffer[i];
                    hashcode += hashcode << 10;
                    hashcode ^= hashcode >> 6;
                }

                hashcode += hashcode << 3;
                hashcode ^= hashcode >> 11;
                hashcode += hashcode << 15;
                return hashcode;
            }
        }
    }
}
