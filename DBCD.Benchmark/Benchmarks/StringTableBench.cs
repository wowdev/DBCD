using System.Buffers;
using BenchmarkDotNet.Attributes;
using System.Text;

namespace DBCD.Benchmark.Benchmarks
{
    [MemoryDiagnoser]
    public class StringTableBench
    {
        private static byte[] InputBytes = File.ReadAllBytes("E:\\stringtable.bytes");
        private static int StringTableSize = (int)InputBytes.Length;

        [Benchmark]
        public void OldMethod()
        {
            using (var stream = new MemoryStream(InputBytes))
            using (var reader = new BinaryReader(stream))
            {
                var StringTable = new Dictionary<long, string>(StringTableSize / 0x20);
                for (int i = 0; i < StringTableSize;)
                {
                    long oldPos = reader.BaseStream.Position;
                    StringTable[i] = reader.ReadCString();
                    i += (int)(reader.BaseStream.Position - oldPos);
                }
            }
        }

        [Benchmark]
        public void NewMethod()
        {
            using (var stream = new MemoryStream(InputBytes))
            using (var reader = new BinaryReader(stream))
            {
                var StringTable = reader.ReadStringTable(StringTableSize);
            }
        }
    }

    public static class BinaryReaderExtensions
    {
        public static string ReadCString(this BinaryReader reader)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0)
                bytes.Add(b);

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public static Dictionary<long, string> ReadStringTable(this BinaryReader reader, int stringTableSize, int baseOffset = 0, bool usePos = false)
        {
            if (stringTableSize == 0)
                return [];

            var stringTable = new Dictionary<long, string>(stringTableSize / 0x20);

            byte[] stringTableBytes = ArrayPool<byte>.Shared.Rent(stringTableSize); // may return a lager buffer than requested
            Span<byte> bufferSpan = stringTableBytes.AsSpan(0, stringTableSize);
            _ = reader.Read(bufferSpan);

            try
            {
                int start = 0;
                for (int i = 0; i < bufferSpan.Length; ++i)
                {
                    if (stringTableBytes[i] == 0)
                    {
                        string str = Encoding.UTF8.GetString(bufferSpan.Slice(start, i - start));
                        if (usePos)
                            stringTable[reader.BaseStream.Position - stringTableSize + start] = str;
                        else
                            stringTable[baseOffset + start] = str;

                        start = i + 1;
                    }
                }

                // Trailing string
                if (start < bufferSpan.Length)
                {
                    string str = Encoding.UTF8.GetString(bufferSpan.Slice(start));
                    if (usePos)
                        stringTable[reader.BaseStream.Position - stringTableSize + start] = str;
                    else
                        stringTable[baseOffset + start] = str;
                }

                return stringTable;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(stringTableBytes);
            }
        }
    }
}
