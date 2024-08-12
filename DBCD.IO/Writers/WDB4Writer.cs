using DBCD.IO.Common;
using DBCD.IO.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DBCD.IO.Writers
{
    class WDB4RowSerializer<T> : IDBRowSerializer<T> where T : class
    {
        public IDictionary<int, BitWriter> Records { get; private set; }

        private readonly BaseWriter<T> m_writer;


        public WDB4RowSerializer(BaseWriter<T> writer)
        {
            m_writer = writer;

            Records = new Dictionary<int, BitWriter>();
        }

        public void Serialize(IDictionary<int, T> rows)
        {
            foreach (var row in rows)
                Serialize(row.Key, row.Value);
        }

        public void Serialize(int id, T row)
        {
            BitWriter bitWriter = new BitWriter(m_writer.RecordSize);

            for (int i = 0; i < m_writer.FieldCache.Length; i++)
            {
                FieldCache<T> info = m_writer.FieldCache[i];

                if (info.IndexMapField && m_writer.Flags.HasFlagExt(DB2Flags.Index))
                    continue;

                if (info.IsArray)
                {
                    if (arrayWriters.TryGetValue(info.FieldType, out var writer))
                        writer(bitWriter, m_writer, (Array)info.Getter(row));
                    else
                        throw new Exception("Unhandled array type: " + typeof(T).Name);
                }
                else
                {
                    if (simpleWriters.TryGetValue(info.FieldType, out var writer))
                        writer(bitWriter, m_writer, info.Getter(row));
                    else
                        throw new Exception("Unhandled field type: " + typeof(T).Name);
                }
            }

            // pad to record size
            if (!m_writer.Flags.HasFlagExt(DB2Flags.Sparse))
                bitWriter.Resize(m_writer.RecordSize);
            else
                bitWriter.ResizeToMultiple(4);

            Records[id] = bitWriter;
        }

        public void GetCopyRows()
        {
            var copydata = Records.GroupBy(x => x.Value).Where(x => x.Count() > 1);
            foreach (var copygroup in copydata)
            {
                int key = copygroup.First().Key;
                foreach (var copy in copygroup.Skip(1))
                    m_writer.CopyData[copy.Key] = key;
            }
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
            [typeof(string)] = (data, writer, value) =>
            {
                if (writer.Flags.HasFlagExt(DB2Flags.Sparse))
                    data.WriteCStringAligned((string)value);
                else
                    WriteFieldValue<int>(data, writer.InternString((string)value));
            }
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

    class WDB4Writer<T> : BaseWriter<T> where T : class
    {
        private const uint WDB4FmtSig = 0x34424457; // WDB4

        public WDB4Writer(WDB4Reader reader, IDictionary<int, T> storage, Stream stream) : base(reader)
        {
            // always 2 empties
            StringTableSize++;

            WDB4RowSerializer<T> serializer = new WDB4RowSerializer<T>(this);
            serializer.Serialize(storage);
            serializer.GetCopyRows();

            RecordsCount = serializer.Records.Count - CopyData.Count;

            using (var writer = new BinaryWriter(stream))
            {
                int minIndex = storage.Keys.MinOrDefault();
                int maxIndex = storage.Keys.MaxOrDefault();
                int copyTableSize = Flags.HasFlagExt(DB2Flags.Sparse) ? 0 : CopyData.Count * 8;

                writer.Write(WDB4FmtSig);
                writer.Write(RecordsCount);
                writer.Write(FieldsCount);
                writer.Write(RecordSize);
                writer.Write(StringTableSize); // if flags & 0x01 != 0, offset to the offset_map
                writer.Write(reader.TableHash);
                writer.Write(reader.Build);
                writer.Write((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                writer.Write(minIndex);
                writer.Write(maxIndex);
                writer.Write(reader.Locale);
                writer.Write(copyTableSize);
                writer.Write((uint)Flags);

                if (storage.Count == 0)
                    return;

                // record data
                uint recordsOffset = (uint)writer.BaseStream.Position;
                foreach (var record in serializer.Records)
                    if (!CopyData.ContainsKey(record.Key))
                        record.Value.CopyTo(writer.BaseStream);

                // string table
                if (!Flags.HasFlagExt(DB2Flags.Sparse))
                {
                    writer.WriteCString("");
                    foreach (var str in StringTable)
                        writer.WriteCString(str.Key);
                }

                // sparse data
                if (Flags.HasFlagExt(DB2Flags.Sparse))
                {
                    // change the StringTableSize to the offset_map position
                    long oldPos = writer.BaseStream.Position;
                    writer.BaseStream.Position = 16;
                    writer.Write((uint)oldPos);
                    writer.BaseStream.Position = oldPos;

                    WriteOffsetRecords(writer, serializer, recordsOffset, maxIndex - minIndex + 1);
                }

                // secondary key
                if (Flags.HasFlagExt(DB2Flags.SecondaryKey))
                    WriteSecondaryKeyData(writer, storage, maxIndex - minIndex + 1);

                // index table
                if (Flags.HasFlagExt(DB2Flags.Index))
                    writer.WriteArray(serializer.Records.Keys.Except(CopyData.Keys).ToArray());

                // copy table
                if (!Flags.HasFlagExt(DB2Flags.Sparse))
                {
                    foreach (var copyRecord in CopyData)
                    {
                        writer.Write(copyRecord.Key);
                        writer.Write(copyRecord.Value);
                    }
                }
            }
        }
    }
}
