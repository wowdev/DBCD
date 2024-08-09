using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace DBCD.IO.Common
{
    class BitWriter : IEquatable<BitWriter>, IDisposable
    {
        private static readonly ArrayPool<byte> SharedPool = ArrayPool<byte>.Create();

        public int TotalBytesWrittenOut { get; private set; }

        private byte AccumulatedBitsCount;
        private byte[] Buffer;

        public BitWriter(int capacity) => Buffer = SharedPool.Rent(capacity);

        public byte this[int i] => Buffer[i];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteAligned<T>(T value) where T : struct
        {
            EnsureSize();
            Unsafe.WriteUnaligned(ref Buffer[TotalBytesWrittenOut], value);
            TotalBytesWrittenOut += Unsafe.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCStringAligned(string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value);
            Array.Resize(ref data, data.Length + 1);

            EnsureSize(data.Length);
            Unsafe.CopyBlockUnaligned(ref Buffer[TotalBytesWrittenOut], ref data[0], (uint)data.Length);

            TotalBytesWrittenOut += data.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(T value, int nbits) where T : struct
        {
            Span<byte> pool = stackalloc byte[0x10];
            if (AccumulatedBitsCount == 0 && (nbits & 7) == 0)
            {
                EnsureSize();
                Unsafe.WriteUnaligned(ref Buffer[TotalBytesWrittenOut], value);
                TotalBytesWrittenOut += nbits / 8;
            }
            else
            {
                Unsafe.WriteUnaligned(ref pool[0], value);
                for (int i = 0; nbits > 0; i++)
                {
                    WriteBits(Math.Min(nbits, 8), pool[i]);
                    nbits -= 8;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(T value, int nbits, int offset) where T : struct
        {
            Span<byte> pool = stackalloc byte[0x10];
            Unsafe.WriteUnaligned(ref pool[0], value);

            int byteOffset = offset >> 3;
            int lowLen = offset & 7;
            int highLen = 8 - lowLen;

            int i = 0;
            while ((nbits -= 8) >= 0)
            {
                // write last part of this byte
                Buffer[byteOffset] = (byte)((Buffer[byteOffset] & (0xFF >> highLen)) | (pool[i] << lowLen));

                // write first part of next byte
                byteOffset++;
                Buffer[byteOffset] = (byte)((Buffer[byteOffset] & (0xFF << lowLen)) | (pool[i] >> highLen));
                i++;
            }

            // write final bits
            if ((nbits &= 7) > 0)
            {
                lowLen = nbits;
                highLen = 8 - nbits;

                Buffer[byteOffset] = (byte)((Buffer[byteOffset] & (0xFF >> highLen)) | (pool[i] << lowLen));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCString(string value)
        {
            // Note: cstrings are always aligned to 8 bytes
            if (AccumulatedBitsCount == 0)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteBits(int bitCount, uint value)
        {
            EnsureSize();

            for (int i = 0; i < bitCount; i++)
            {
                Buffer[TotalBytesWrittenOut] |= (byte)(((value >> i) & 0x1) << AccumulatedBitsCount);
                AccumulatedBitsCount++;

                if (AccumulatedBitsCount > 7)
                {
                    TotalBytesWrittenOut++;
                    AccumulatedBitsCount = 0;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureSize(int size = 8)
        {
            if (TotalBytesWrittenOut + size >= Buffer.Length)
            {
                byte[] rent = SharedPool.Rent(Buffer.Length + size);

                Unsafe.CopyBlockUnaligned(ref rent[0], ref Buffer[0], (uint)rent.Length);

                SharedPool.Return(Buffer, true);

                Buffer = rent;
            }
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
                EnsureSize(divisor);
                TotalBytesWrittenOut += divisor - remainder;
            }
        }

        public void CopyTo(Stream stream)
        {
            stream.Write(Buffer, 0, TotalBytesWrittenOut);
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
                const int p = 16777619;
                int hash = (int)2166136261;

                for (int i = 0; i < TotalBytesWrittenOut; i++)
                    hash = (hash ^ Buffer[i]) * p;

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        public void Dispose() => SharedPool.Return(Buffer);
    }
}