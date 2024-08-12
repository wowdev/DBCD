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
            var StringTable = new Dictionary<long, string>(stringTableSize / 0x20);

            if (stringTableSize == 0)
                return StringTable;

            var curOfs = 0;
            var decoded = Encoding.UTF8.GetString(reader.ReadBytes(stringTableSize));
            foreach (var str in decoded.Split('\0'))
            {
                if (curOfs == stringTableSize)
                    break;

                if (usePos)
                    StringTable[(reader.BaseStream.Position - stringTableSize) + curOfs] = str;
                else
                    StringTable[baseOffset + curOfs] = str;

                curOfs += Encoding.UTF8.GetByteCount(str) + 1;
            }

            return StringTable;
        }
    }
}
