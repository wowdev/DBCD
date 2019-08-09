using DBCD.IO.Common;
using DBCD.IO.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DBCD.IO.Writers
{
    class WDB2RowSerializer<T> : IDBRowSerializer<T> where T : class
    {
        public IDictionary<int, BitWriter> Records { get; private set; }
        public IDictionary<int, ushort> StringLengths { get; private set; }

        private readonly BaseWriter<T> m_writer;


        public WDB2RowSerializer(BaseWriter<T> writer)
        {
            m_writer = writer;

            Records = new Dictionary<int, BitWriter>();
            StringLengths = new Dictionary<int, ushort>();
        }

        public void Serialize(IDictionary<int, T> rows)
        {
            foreach (var row in rows)
                Serialize(row.Key, row.Value);
        }

        public void Serialize(int id, T row)
        {
            BitWriter bitWriter = new BitWriter(m_writer.RecordSize);
            StringLengths[id] = 0;

            for (int i = 0; i < m_writer.FieldCache.Length; i++)
            {
                FieldCache<T> info = m_writer.FieldCache[i];

                if (info.IsArray)
                {
                    if (arrayWriters.TryGetValue(info.Field.FieldType, out var writer))
                    {
                        Array array = (Array)info.Getter(row);
                        writer(bitWriter, m_writer, array);

                        if (array is string[] strings)
                            StringLengths[id] = (ushort)strings.Sum(x => x.Length == 0 ? 0 : x.Length + 1);
                    }
                    else
                        throw new Exception("Unhandled array type: " + typeof(T).Name);
                }
                else
                {
                    if (simpleWriters.TryGetValue(info.Field.FieldType, out var writer))
                    {
                        object value = info.Getter(row);
                        writer(bitWriter, m_writer, value);

                        if (value is string strings)
                            StringLengths[id] = (ushort)(strings.Length == 0 ? 0 : strings.Length + 1);
                    }
                    else
                        throw new Exception("Unhandled field type: " + typeof(T).Name);
                }
            }

            // pad to record size
            bitWriter.Resize(m_writer.RecordSize);
            Records[id] = bitWriter;
        }

        public void GetCopyRows()
        {
            throw new NotImplementedException();
        }


        private static Dictionary<Type, Action<BitWriter, BaseWriter<T>, object>> simpleWriters = new Dictionary<Type, Action<BitWriter, BaseWriter<T>, object>>
        {
            [typeof(long)] = (data, writer, value) => WriteFieldValue<long>(data, value),
            [typeof(float)] = (data, writer, value) => WriteFieldValue<float>(data, value),
            [typeof(int)] = (data, writer, value) => WriteFieldValue<int>(data, value),
            [typeof(uint)] = (data, writer, value) => WriteFieldValue<uint>(data, value),
            [typeof(short)] = (data, writer, value) => WriteFieldValue<short>(data, value),
            [typeof(ushort)] = (data, writer, value) => WriteFieldValue<ushort>(data, value),
            [typeof(sbyte)] = (data, writer, value) => WriteFieldValue<sbyte>(data, value),
            [typeof(byte)] = (data, writer, value) => WriteFieldValue<byte>(data, value),
            [typeof(string)] = (data, writer, value) => WriteFieldValue<int>(data, writer.InternString((string)value)),
        };

        private readonly Dictionary<Type, Action<BitWriter, BaseWriter<T>, Array>> arrayWriters = new Dictionary<Type, Action<BitWriter, BaseWriter<T>, Array>>
        {
            [typeof(ulong[])] = (data, writer, array) => WriteFieldValueArray<ulong>(data, array),
            [typeof(long[])] = (data, writer, array) => WriteFieldValueArray<long>(data, array),
            [typeof(float[])] = (data, writer, array) => WriteFieldValueArray<float>(data, array),
            [typeof(int[])] = (data, writer, array) => WriteFieldValueArray<int>(data, array),
            [typeof(uint[])] = (data, writer, array) => WriteFieldValueArray<uint>(data, array),
            [typeof(ulong[])] = (data, writer, array) => WriteFieldValueArray<ulong>(data, array),
            [typeof(ushort[])] = (data, writer, array) => WriteFieldValueArray<ushort>(data, array),
            [typeof(short[])] = (data, writer, array) => WriteFieldValueArray<short>(data, array),
            [typeof(byte[])] = (data, writer, array) => WriteFieldValueArray<byte>(data, array),
            [typeof(sbyte[])] = (data, writer, array) => WriteFieldValueArray<sbyte>(data, array),
            [typeof(string[])] = (data, writer, array) => WriteFieldValueArray<int>(data, (array as string[]).Select(x => writer.InternString(x)).ToArray()),
        };

        private static void WriteFieldValue<TType>(BitWriter r, object value) where TType : struct
        {
            r.WriteAligned((TType)value);
        }

        private static void WriteFieldValueArray<TType>(BitWriter r, Array value) where TType : struct
        {
            for (int i = 0; i < value.Length; i++)
                r.WriteAligned((TType)value.GetValue(i));
        }

    }

    class WDB2Writer<T> : BaseWriter<T> where T : class
    {
        private const uint WDB2FmtSig = 0x32424457; // WDB2

        public WDB2Writer(WDB2Reader reader, IDictionary<int, T> storage, Stream stream) : base(reader)
        {
            WDB2RowSerializer<T> serializer = new WDB2RowSerializer<T>(this);
            serializer.Serialize(storage);

            RecordsCount = storage.Count;

            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(WDB2FmtSig);
                writer.Write(RecordsCount);
                writer.Write(FieldsCount);
                writer.Write(RecordSize);
                writer.Write(StringTableSize);
                writer.Write(reader.TableHash);
                writer.Write(reader.Build);
                writer.Write((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                if (storage.Count == 0)
                    return;

                // Extended header
                if (reader.Build > 12880)
                {
                    if (reader.MaxIndex == 0)
                    {
                        writer.Write(0);
                        writer.Write(0);
                        writer.Write(reader.LayoutHash);
                        writer.Write(0); // CopyTableSize
                    }
                    else
                    {
                        WriteIndices(writer, serializer, reader.LayoutHash);
                    }
                }

                foreach (var record in serializer.Records)
                    record.Value.CopyTo(writer.BaseStream);

                foreach (var str in m_stringsTable)
                    writer.WriteCString(str.Key);
            }
        }

        private void WriteIndices(BinaryWriter writer, WDB2RowSerializer<T> serializer, uint layoutHash)
        {
            int min = serializer.Records.Keys.Min();
            int max = serializer.Records.Keys.Max();

            writer.Write(min);
            writer.Write(max);
            writer.Write(layoutHash);
            writer.Write(0); // CopyTableSize

            int index = 0;
            for (int i = min; i <= max; i++)
            {
                if (serializer.StringLengths.ContainsKey(i))
                {
                    writer.Write(++index);
                    writer.Write(serializer.StringLengths[i]);
                }
                else
                {
                    writer.Write(0);
                    writer.Write((ushort)0);
                }
            }
        }
    }
}
