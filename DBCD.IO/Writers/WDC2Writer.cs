using DBCD.IO.Common;
using DBCD.IO.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DBCD.IO.Writers
{
    class WDC2RowSerializer<T> : IDBRowSerializer<T> where T : class
    {
        public IDictionary<int, BitWriter> Records { get; private set; }

        private readonly BaseWriter<T> m_writer;
        private readonly FieldMetaData[] m_fieldMeta;
        private readonly ColumnMetaData[] ColumnMeta;
        private readonly OrderedHashSet<Value32[]>[] PalletData;
        private readonly Dictionary<int, Value32>[] CommonData;

        private static readonly Value32Comparer Value32Comparer = new Value32Comparer();


        public WDC2RowSerializer(BaseWriter<T> writer)
        {
            m_writer = writer;
            m_fieldMeta = m_writer.Meta;
            ColumnMeta = m_writer.ColumnMeta;
            PalletData = m_writer.PalletData;
            CommonData = m_writer.CommonData;

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

                if (i == m_writer.IdFieldIndex && m_writer.Flags.HasFlagExt(DB2Flags.Index))
                {
                    indexFieldOffSet++;
                    continue;
                }

                int fieldIndex = i - indexFieldOffSet;

                // relationship field, used for faster lookup on IDs
                if (info.IsRelation)
                    m_writer.ReferenceData.Add((int)Convert.ChangeType(info.Getter(row), typeof(int)));

                if (info.IsArray)
                {
                    if (arrayWriters.TryGetValue(info.FieldType, out var writer))
                        writer(bitWriter, m_writer, m_fieldMeta[fieldIndex], ColumnMeta[fieldIndex], PalletData[fieldIndex], CommonData[fieldIndex], (Array)info.Getter(row));
                    else
                        throw new Exception("Unhandled array type: " + typeof(T).Name);
                }
                else
                {
                    if (simpleWriters.TryGetValue(info.FieldType, out var writer))
                        writer(id, bitWriter, m_writer, m_fieldMeta[fieldIndex], ColumnMeta[fieldIndex], PalletData[fieldIndex], CommonData[fieldIndex], info.Getter(row));
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

        public void UpdateStringOffsets(IDictionary<int, T> rows)
        {
            if (m_writer.Flags.HasFlagExt(DB2Flags.Sparse) || m_writer.StringTableSize <= 1)
                return;

            int indexFieldOffSet = 0;
            var fieldInfos = new Dictionary<int, FieldCache<T>>();
            for (int i = 0; i < m_writer.FieldCache.Length; i++)
            {
                if (i == m_writer.IdFieldIndex && m_writer.Flags.HasFlagExt(DB2Flags.Index))
                    indexFieldOffSet++;
                else if (m_writer.FieldCache[i].Field.FieldType == typeof(string))
                    fieldInfos[i - indexFieldOffSet] = m_writer.FieldCache[i];
                else if (m_writer.FieldCache[i].Field.FieldType == typeof(string[]))
                    fieldInfos[i - indexFieldOffSet] = m_writer.FieldCache[i];
            }

            if (fieldInfos.Count == 0)
                return;

            int recordOffset = (Records.Count - m_writer.CopyData.Count) * m_writer.RecordSize;
            int fieldOffset = 0;

            foreach (var record in Records)
            {
                // skip copy records
                if (m_writer.CopyData.ContainsKey(record.Key))
                    continue;

                foreach (var fieldInfo in fieldInfos)
                {
                    int index = fieldInfo.Key;
                    var info = fieldInfo.Value;

                    var columnMeta = ColumnMeta[index];
                    if (columnMeta.CompressionType != CompressionType.None)
                        throw new Exception("CompressionType != CompressionType.None");

                    int bitSize = 32 - m_fieldMeta[index].Bits;
                    if (bitSize <= 0)
                        bitSize = columnMeta.Immediate.BitWidth;

                    if (info.IsArray)
                    {
                        var array = (string[])info.Getter(rows[record.Key]);
                        for (int i = 0; i < array.Length; i++)
                        {
                            fieldOffset = m_writer.StringTable[array[i]] + (recordOffset) - (sizeof(int) * i) - (columnMeta.RecordOffset / 8);
                            record.Value.Write(fieldOffset, bitSize, columnMeta.RecordOffset + (i * bitSize));
                        }
                    }
                    else
                    {
                        fieldOffset = m_writer.StringTable[(string)info.Getter(rows[record.Key])] + recordOffset - (columnMeta.RecordOffset / 8);
                        record.Value.Write(fieldOffset, bitSize, columnMeta.RecordOffset);
                    }
                }

                recordOffset -= m_writer.RecordSize;
            }
        }


        private static Dictionary<Type, Action<int, BitWriter, BaseWriter<T>, FieldMetaData, ColumnMetaData, OrderedHashSet<Value32[]>, Dictionary<int, Value32>, object>> simpleWriters = new Dictionary<Type, Action<int, BitWriter, BaseWriter<T>, FieldMetaData, ColumnMetaData, OrderedHashSet<Value32[]>, Dictionary<int, Value32>, object>>
        {
            [typeof(ulong)] = (id, data, writer, fieldMeta, columnMeta, palletData, commonData, value) => WriteFieldValue<ulong>(id, data, fieldMeta, columnMeta, palletData, commonData, value),
            [typeof(long)] = (id, data, writer, fieldMeta, columnMeta, palletData, commonData, value) => WriteFieldValue<long>(id, data, fieldMeta, columnMeta, palletData, commonData, value),
            [typeof(float)] = (id, data, writer, fieldMeta, columnMeta, palletData, commonData, value) => WriteFieldValue<float>(id, data, fieldMeta, columnMeta, palletData, commonData, value),
            [typeof(int)] = (id, data, writer, fieldMeta, columnMeta, palletData, commonData, value) => WriteFieldValue<int>(id, data, fieldMeta, columnMeta, palletData, commonData, value),
            [typeof(uint)] = (id, data, writer, fieldMeta, columnMeta, palletData, commonData, value) => WriteFieldValue<uint>(id, data, fieldMeta, columnMeta, palletData, commonData, value),
            [typeof(short)] = (id, data, writer, fieldMeta, columnMeta, palletData, commonData, value) => WriteFieldValue<short>(id, data, fieldMeta, columnMeta, palletData, commonData, value),
            [typeof(ushort)] = (id, data, writer, fieldMeta, columnMeta, palletData, commonData, value) => WriteFieldValue<ushort>(id, data, fieldMeta, columnMeta, palletData, commonData, value),
            [typeof(sbyte)] = (id, data, writer, fieldMeta, columnMeta, palletData, commonData, value) => WriteFieldValue<sbyte>(id, data, fieldMeta, columnMeta, palletData, commonData, value),
            [typeof(byte)] = (id, data, writer, fieldMeta, columnMeta, palletData, commonData, value) => WriteFieldValue<byte>(id, data, fieldMeta, columnMeta, palletData, commonData, value),
            [typeof(string)] = (id, data, writer, fieldMeta, columnMeta, palletData, commonData, value) =>
            {
                if (writer.Flags.HasFlagExt(DB2Flags.Sparse))
                    data.WriteCString((string)value);
                else
                    WriteFieldValue<int>(id, data, fieldMeta, columnMeta, palletData, commonData, writer.InternString((string)value));
            }
        };

        private static Dictionary<Type, Action<BitWriter, BaseWriter<T>, FieldMetaData, ColumnMetaData, OrderedHashSet<Value32[]>, Dictionary<int, Value32>, Array>> arrayWriters = new Dictionary<Type, Action<BitWriter, BaseWriter<T>, FieldMetaData, ColumnMetaData, OrderedHashSet<Value32[]>, Dictionary<int, Value32>, Array>>
        {
            [typeof(ulong[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<ulong>(data, fieldMeta, columnMeta, palletData, commonData, array),
            [typeof(long[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<long>(data, fieldMeta, columnMeta, palletData, commonData, array),
            [typeof(float[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<float>(data, fieldMeta, columnMeta, palletData, commonData, array),
            [typeof(int[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<int>(data, fieldMeta, columnMeta, palletData, commonData, array),
            [typeof(uint[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<uint>(data, fieldMeta, columnMeta, palletData, commonData, array),
            [typeof(ulong[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<ulong>(data, fieldMeta, columnMeta, palletData, commonData, array),
            [typeof(ushort[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<ushort>(data, fieldMeta, columnMeta, palletData, commonData, array),
            [typeof(short[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<short>(data, fieldMeta, columnMeta, palletData, commonData, array),
            [typeof(byte[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<byte>(data, fieldMeta, columnMeta, palletData, commonData, array),
            [typeof(sbyte[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<sbyte>(data, fieldMeta, columnMeta, palletData, commonData, array),
            [typeof(string[])] = (data, writer, fieldMeta, columnMeta, palletData, commonData, array) => WriteFieldValueArray<int>(data, fieldMeta, columnMeta, palletData, commonData, (array as string[]).Select(x => writer.InternString(x)).ToArray()),
        };

        private static void WriteFieldValue<TType>(int Id, BitWriter r, FieldMetaData fieldMeta, ColumnMetaData columnMeta, OrderedHashSet<Value32[]> palletData, Dictionary<int, Value32> commonData, object value) where TType : unmanaged
        {
            switch (columnMeta.CompressionType)
            {
                case CompressionType.None:
                    {
                        int bitSize = 32 - fieldMeta.Bits;
                        if (bitSize <= 0)
                            bitSize = columnMeta.Immediate.BitWidth;

                        r.Write((TType)value, bitSize);
                        break;
                    }
                case CompressionType.Immediate:
                case CompressionType.SignedImmediate:
                    {
                        r.Write((TType)value, columnMeta.Immediate.BitWidth);
                        break;
                    }
                case CompressionType.Common:
                    {
                        if (!columnMeta.Common.DefaultValue.GetValue<TType>().Equals(value))
                            commonData.Add(Id, Value32.Create((TType)value));
                        break;
                    }
                case CompressionType.Pallet:
                    {
                        Value32[] array = new[] { Value32.Create(value) };

                        int palletIndex = palletData.IndexOf(array);
                        if (palletIndex == -1)
                        {
                            palletIndex = palletData.Count;
                            palletData.Add(array);
                        }

                        r.Write(palletIndex, columnMeta.Pallet.BitWidth);
                        break;
                    }
            }
        }

        private static void WriteFieldValueArray<TType>(BitWriter r, FieldMetaData fieldMeta, ColumnMetaData columnMeta, OrderedHashSet<Value32[]> palletData, Dictionary<int, Value32> commonData, Array value) where TType : unmanaged
        {
            switch (columnMeta.CompressionType)
            {
                case CompressionType.None:
                    {
                        int bitSize = 32 - fieldMeta.Bits;
                        if (bitSize <= 0)
                            bitSize = columnMeta.Immediate.BitWidth;

                        for (int i = 0; i < value.Length; i++)
                            r.Write((TType)value.GetValue(i), bitSize);

                        break;
                    }
                case CompressionType.PalletArray:
                    {
                        // get data
                        Value32[] array = new Value32[value.Length];
                        for (int i = 0; i < value.Length; i++)
                            array[i] = Value32.Create(value.GetValue(i));

                        int palletIndex = palletData.IndexOf(array);
                        if (palletIndex == -1)
                        {
                            palletIndex = palletData.Count;
                            palletData.Add(array);
                        }

                        r.Write(palletIndex, columnMeta.Pallet.BitWidth);
                        break;
                    }
            }
        }
    }

    class WDC2Writer<T> : BaseWriter<T> where T : class
    {
        private const int HeaderSize = 72;

        public WDC2Writer(WDC2Reader reader, IDictionary<int, T> storage, Stream stream) : base(reader)
        {
            // always 2 empties
            StringTableSize++;

            PackedDataOffset = reader.PackedDataOffset;
            var (commonDataSize, palletDataSize, referenceDataSize) = (0, 0, 0);
            if (ColumnMeta != null)
                HandleCompression(storage);

            WDC2RowSerializer<T> serializer = new WDC2RowSerializer<T>(this);
            serializer.Serialize(storage);

            // We write the copy rows if and only if it saves space and the table hasn't any reference rows.
            if ((RecordSize) >= sizeof(int) * 2 && ReferenceData.Count == 0)
                serializer.GetCopyRows();

            serializer.UpdateStringOffsets(storage);

            RecordsCount = serializer.Records.Count - CopyData.Count;

            if (ColumnMeta != null)
                (commonDataSize, palletDataSize, referenceDataSize) = GetDataSizes();

            using (var writer = new BinaryWriter(stream))
            {
                int minIndex = storage.Keys.MinOrDefault();
                int maxIndex = storage.Keys.MaxOrDefault();
                int copyTableSize = Flags.HasFlagExt(DB2Flags.Sparse) ? 0 : CopyData.Count * 8;

                writer.Write(reader.Signature);
                writer.Write(RecordsCount);
                writer.Write(FieldsCount);
                writer.Write(RecordSize);
                writer.Write(StringTableSize);
                writer.Write(reader.TableHash);
                writer.Write(reader.LayoutHash);
                writer.Write(minIndex);
                writer.Write(maxIndex);
                writer.Write(reader.Locale);
                writer.Write((ushort)Flags);
                writer.Write((ushort)IdFieldIndex);

                writer.Write(FieldsCount); // totalFieldCount
                writer.Write(PackedDataOffset);
                writer.Write(ReferenceData != null && ReferenceData.Count > 0 ? 1 : 0); // RelationshipColumnCount
                writer.Write(ColumnMeta != null && ColumnMeta.Length > 0 ? ColumnMeta.Length * 24 : 0); // ColumnMetaDataSize
                writer.Write(commonDataSize);
                writer.Write(palletDataSize);
                writer.Write(1); // sections count

                if (storage.Count == 0)
                    return;

                // section header
                int fileOffset = HeaderSize + (Meta.Length * 4) + (ColumnMeta.Length * 24) + Unsafe.SizeOf<SectionHeader>() + palletDataSize + commonDataSize;

                writer.Write(0UL); // TactKeyLookup
                writer.Write(fileOffset); // FileOffset
                writer.Write(RecordsCount); // NumRecords
                writer.Write(StringTableSize);
                writer.Write(copyTableSize);
                writer.Write(0); // sparseTableOffset
                writer.Write(Flags.HasFlagExt(DB2Flags.Index) ? RecordsCount * 4 : 0);  // IndexDataSize
                writer.Write(referenceDataSize);

                // field meta
                writer.WriteArray(Meta);

                // column meta data
                writer.WriteArray(ColumnMeta);

                // pallet data
                for (int i = 0; i < ColumnMeta.Length; i++)
                {
                    if (ColumnMeta[i].CompressionType == CompressionType.Pallet || ColumnMeta[i].CompressionType == CompressionType.PalletArray)
                    {
                        foreach (var palletData in PalletData[i])
                            writer.WriteArray(palletData);
                    }
                }

                // common data
                for (int i = 0; i < ColumnMeta.Length; i++)
                {
                    if (ColumnMeta[i].CompressionType == CompressionType.Common)
                    {
                        foreach (var commondata in CommonData[i])
                        {
                            writer.Write(commondata.Key);
                            writer.Write(commondata.Value.GetValue<int>());
                        }
                    }
                }

                // record data
                uint recordsOffset = (uint)writer.BaseStream.Position;
                foreach (var record in serializer.Records)
                    if (!CopyData.TryGetValue(record.Key, out int parent))
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
                    // set the sparseTableOffset
                    long oldPos = writer.BaseStream.Position;
                    writer.BaseStream.Position = 96;
                    writer.Write((uint)oldPos);
                    writer.BaseStream.Position = oldPos;

                    WriteOffsetRecords(writer, serializer, recordsOffset, maxIndex - minIndex + 1);
                }

                // index table
                if (Flags.HasFlagExt(DB2Flags.Index))
                    writer.WriteArray(serializer.Records.Keys.Except(CopyData.Keys).ToArray());

                // copy table
                if (!Flags.HasFlagExt(DB2Flags.Sparse))
                {
                    foreach (var copyRecord in CopyData.OrderBy(r => r.Value))
                    {
                        writer.Write(copyRecord.Key);
                        writer.Write(copyRecord.Value);
                    }
                }

                // reference data
                if (ReferenceData.Count > 0)
                {
                    writer.Write(ReferenceData.Count);
                    writer.Write(ReferenceData.Min());
                    writer.Write(ReferenceData.Max());

                    for (int i = 0; i < ReferenceData.Count; i++)
                    {
                        writer.Write(ReferenceData[i]);
                        writer.Write(i);
                    }
                }
            }
        }

        private (int CommonDataSize, int PalletDataSize, int RefDataSize) GetDataSizes()
        {
            // uint NumRecords, uint minId, uint maxId, {uint id, uint index}[NumRecords]
            int refSize = 0;
            if (ReferenceData.Count > 0)
                refSize = 12 + (ReferenceData.Count * 8);

            int commonSize = 0, palletSize = 0;
            for (int i = 0; i < ColumnMeta.Length; i++)
            {
                switch (ColumnMeta[i].CompressionType)
                {
                    // {uint id, uint copyid}[]
                    case CompressionType.Common:
                        ColumnMeta[i].AdditionalDataSize = (uint)(CommonData[i].Count * 8);
                        commonSize += (int)ColumnMeta[i].AdditionalDataSize;
                        break;

                    // {uint values[cardinality]}[]
                    case CompressionType.Pallet:
                    case CompressionType.PalletArray:
                        ColumnMeta[i].AdditionalDataSize = (uint)PalletData[i].Sum(x => x.Length * 4);
                        palletSize += (int)ColumnMeta[i].AdditionalDataSize;
                        break;
                }
            }

            return (commonSize, palletSize, refSize);
        }
    }
}
