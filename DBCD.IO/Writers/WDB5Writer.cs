using DBCD.IO.Common;
using DBCD.IO.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DBCD.IO.Writers
{
    class WDB5RowSerializer<T> : IDBRowSerializer<T> where T : class
    {
        public IDictionary<int, BitWriter> Records { get; private set; }

        private readonly BaseWriter<T> m_writer;
        private readonly FieldMetaData[] m_fieldMeta;


        public WDB5RowSerializer(BaseWriter<T> writer)
        {
            m_writer = writer;
            m_fieldMeta = m_writer.Meta;

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

            int indexFieldOffSet = 0;

            for (int i = 0; i < m_writer.FieldCache.Length; i++)
            {
                FieldCache<T> info = m_writer.FieldCache[i];

                if (info.IndexMapField && m_writer.Flags.HasFlagExt(DB2Flags.Index))
                {
                    indexFieldOffSet++;
                    continue;
                }

                int fieldIndex = i - indexFieldOffSet;

                if (info.IsArray)
                {
                    if (arrayWriters.TryGetValue(info.Field.FieldType, out var writer))
                        writer(bitWriter, m_writer, m_fieldMeta[fieldIndex], (Array)info.Getter(row));
                    else
                        throw new Exception("Unhandled array type: " + typeof(T).Name);
                }
                else
                {
                    if (simpleWriters.TryGetValue(info.Field.FieldType, out var writer))
                        writer(bitWriter, m_writer, m_fieldMeta[fieldIndex], info.Getter(row));
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


        private static Dictionary<Type, Action<BitWriter, BaseWriter<T>, FieldMetaData, object>> simpleWriters = new Dictionary<Type, Action<BitWriter, BaseWriter<T>, FieldMetaData, object>>
        {
            [typeof(long)] = (data, writer, fieldMeta, value) => WriteFieldValue<long>(data, fieldMeta, value),
            [typeof(float)] = (data, writer, fieldMeta, value) => WriteFieldValue<float>(data, fieldMeta, value),
            [typeof(int)] = (data, writer, fieldMeta, value) => WriteFieldValue<int>(data, fieldMeta, value),
            [typeof(uint)] = (data, writer, fieldMeta, value) => WriteFieldValue<uint>(data, fieldMeta, value),
            [typeof(short)] = (data, writer, fieldMeta, value) => WriteFieldValue<short>(data, fieldMeta, value),
            [typeof(ushort)] = (data, writer, fieldMeta, value) => WriteFieldValue<ushort>(data, fieldMeta, value),
            [typeof(sbyte)] = (data, writer, fieldMeta, value) => WriteFieldValue<sbyte>(data, fieldMeta, value),
            [typeof(byte)] = (data, writer, fieldMeta, value) => WriteFieldValue<byte>(data, fieldMeta, value),
            [typeof(string)] = (data, writer, fieldMeta, value) =>
            {
                if (writer.Flags.HasFlagExt(DB2Flags.Sparse))
                    data.WriteCString((string)value);
                else
                    WriteFieldValue<int>(data, fieldMeta, writer.InternString((string)value));
            }
        };

        private static Dictionary<Type, Action<BitWriter, BaseWriter<T>, FieldMetaData, Array>> arrayWriters = new Dictionary<Type, Action<BitWriter, BaseWriter<T>, FieldMetaData, Array>>
        {
            [typeof(ulong[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<ulong>(data, fieldMeta, array),
            [typeof(long[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<long>(data, fieldMeta, array),
            [typeof(float[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<float>(data, fieldMeta, array),
            [typeof(int[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<int>(data, fieldMeta, array),
            [typeof(uint[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<uint>(data, fieldMeta, array),
            [typeof(ulong[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<ulong>(data, fieldMeta, array),
            [typeof(ushort[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<ushort>(data, fieldMeta, array),
            [typeof(short[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<short>(data, fieldMeta, array),
            [typeof(byte[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<byte>(data, fieldMeta, array),
            [typeof(sbyte[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<sbyte>(data, fieldMeta, array),
            [typeof(string[])] = (data, writer, fieldMeta, array) => WriteFieldValueArray<int>(data, fieldMeta, (array as string[]).Select(x => writer.InternString(x)).ToArray()),
        };

        private static void WriteFieldValue<TType>(BitWriter r, FieldMetaData fieldMeta, object value) where TType : struct
        {
            r.Write((TType)value, 32 - fieldMeta.Bits);
        }

        private static void WriteFieldValueArray<TType>(BitWriter r, FieldMetaData fieldMeta, Array value) where TType : struct
        {
            for (int i = 0; i < value.Length; i++)
                r.Write((TType)value.GetValue(i), 32 - fieldMeta.Bits);
        }
    }

    class WDB5Writer<T> : BaseWriter<T> where T : class
    {
        private const uint WDB5FmtSig = 0x35424457; // WDB5

        public WDB5Writer(WDB5Reader reader, IDictionary<int, T> storage, Stream stream) : base(reader)
        {
            // always 2 empties
            StringTableSize++;

            WDB5RowSerializer<T> serializer = new WDB5RowSerializer<T>(this);
            serializer.Serialize(storage);
            serializer.GetCopyRows();

            RecordsCount = serializer.Records.Count - CopyData.Count;

            using (var writer = new BinaryWriter(stream))
            {
                int minIndex = storage.Keys.Min();
                int maxIndex = storage.Keys.Max();
                int copyTableSize = Flags.HasFlagExt(DB2Flags.Sparse) ? 0 : CopyData.Count * 8;

                writer.Write(WDB5FmtSig);
                writer.Write(RecordsCount);
                writer.Write(FieldsCount);
                writer.Write(RecordSize);
                writer.Write(StringTableSize); // if flags & 0x01 != 0, offset to the offset_map
                writer.Write(reader.TableHash);
                writer.Write(reader.LayoutHash);
                writer.Write(minIndex);
                writer.Write(maxIndex);
                writer.Write(reader.Locale);
                writer.Write(copyTableSize); // copytablesize
                writer.Write((uint)Flags);
                writer.Write((ushort)IdFieldIndex);

                if (storage.Count == 0)
                    return;

                // field meta
                writer.WriteArray(Meta);

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
