using DBCD.IO.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DBCD.IO.Readers
{
    class WDC2Row : IDBRow
    {
        private BitReader m_data;
        private BaseReader m_reader;
        private readonly int m_dataOffset;
        private readonly int m_dataPosition;
        private readonly int m_recordsOffset;
        private readonly int m_recordIndex;

        public int Id { get; set; }
        public BitReader Data { get => m_data; set => m_data = value; }

        private readonly FieldMetaData[] m_fieldMeta;
        private readonly ColumnMetaData[] ColumnMeta;
        private readonly Value32[][] PalletData;
        private readonly Dictionary<int, Value32>[] CommonData;
        private readonly int m_refID;

        public WDC2Row(BaseReader reader, BitReader data, int recordsOffset, int id, int refID, int recordIndex)
        {
            m_reader = reader;
            m_data = data;
            m_recordsOffset = recordsOffset;
            m_recordIndex = recordIndex;

            m_dataOffset = m_data.Offset;
            m_dataPosition = m_data.Position;

            m_fieldMeta = reader.Meta;
            ColumnMeta = reader.ColumnMeta;
            PalletData = reader.PalletData;
            CommonData = reader.CommonData;
            m_refID = refID;

            Id = id;
        }

        private static Dictionary<Type, Func<int, BitReader, int, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, BaseReader, object>> simpleReaders = new Dictionary<Type, Func<int, BitReader, int, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, BaseReader, object>>
        {
            [typeof(ulong)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable, header) => GetFieldValue<ulong>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(long)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable, header) => GetFieldValue<long>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(float)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable, header) => GetFieldValue<float>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(int)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable, header) => GetFieldValue<int>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(uint)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable, header) => GetFieldValue<uint>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(short)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable, header) => GetFieldValue<short>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(ushort)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable, header) => GetFieldValue<ushort>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(sbyte)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable, header) => GetFieldValue<sbyte>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(byte)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable, header) => GetFieldValue<byte>(id, data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(string)] = (id, data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable, header) => header.Flags.HasFlagExt(DB2Flags.Sparse) ? data.ReadCString() :
                stringTable[(recordsOffset + data.Offset + (data.Position >> 3)) + GetFieldValue<int>(id, data, fieldMeta, columnMeta, palletData, commonData)],
        };

        private static Dictionary<Type, Func<BitReader, int, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, object>> arrayReaders = new Dictionary<Type, Func<BitReader, int, FieldMetaData, ColumnMetaData, Value32[], Dictionary<int, Value32>, Dictionary<long, string>, object>>
        {
            [typeof(ulong[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueArray<ulong>(data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(long[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueArray<long>(data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(float[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueArray<float>(data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(int[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueArray<int>(data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(uint[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueArray<uint>(data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(ulong[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueArray<ulong>(data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(short[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueArray<short>(data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(ushort[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueArray<ushort>(data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(byte[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueArray<byte>(data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(sbyte[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueArray<sbyte>(data, fieldMeta, columnMeta, palletData, commonData),
            [typeof(string[])] = (data, recordsOffset, fieldMeta, columnMeta, palletData, commonData, stringTable) => GetFieldValueStringArray(data, fieldMeta, columnMeta, recordsOffset, stringTable),
        };

        public void GetFields<T>(FieldCache<T>[] fields, T entry)
        {
            int indexFieldOffSet = 0;

            m_data.Position = m_dataPosition;
            m_data.Offset = m_dataOffset;

            for (int i = 0; i < fields.Length; i++)
            {
                FieldCache<T> info = fields[i];
                if (i == m_reader.IdFieldIndex)
                {
                    if (Id != -1)
                    {
                        indexFieldOffSet++;
                    }
                    else
                    {
                        if (!m_reader.Flags.HasFlagExt(DB2Flags.Sparse))
                        {
                            m_data.Position = ColumnMeta[i].RecordOffset;
                            m_data.Offset = m_dataOffset;
                        }

                        Id = GetFieldValue<int>(0, m_data, m_fieldMeta[i], ColumnMeta[i], PalletData[i], CommonData[i]);
                    }

                    info.Setter(entry, Convert.ChangeType(Id, info.FieldType));
                    continue;
                }

                object value = null;
                int fieldIndex = i - indexFieldOffSet;

                if (fieldIndex >= m_reader.Meta.Length)
                {
                    info.Setter(entry, Convert.ChangeType(m_refID, info.FieldType));
                    continue;
                }

                if (!m_reader.Flags.HasFlagExt(DB2Flags.Sparse))
                {
                    m_data.Position = ColumnMeta[fieldIndex].RecordOffset;
                    m_data.Offset = m_dataOffset;
                }

                if (info.IsArray)
                {
                    if (arrayReaders.TryGetValue(info.FieldType, out var reader))
                        value = reader(m_data, m_recordsOffset, m_fieldMeta[fieldIndex], ColumnMeta[fieldIndex], PalletData[fieldIndex], CommonData[fieldIndex], m_reader.StringTable);
                    else
                        throw new Exception("Unhandled array type: " + typeof(T).Name);
                }
                else
                {
                    if (simpleReaders.TryGetValue(info.FieldType, out var reader))
                        value = reader(Id, m_data, m_recordsOffset, m_fieldMeta[fieldIndex], ColumnMeta[fieldIndex], PalletData[fieldIndex], CommonData[fieldIndex], m_reader.StringTable, m_reader);
                    else
                        throw new Exception("Unhandled field type: " + typeof(T).Name);
                }

                info.Setter(entry, value);
            }
        }

        private static T GetFieldValue<T>(int Id, BitReader r, FieldMetaData fieldMeta, ColumnMetaData columnMeta, Value32[] palletData, Dictionary<int, Value32> commonData) where T : struct
        {
            switch (columnMeta.CompressionType)
            {
                case CompressionType.None:
                    {
                        int bitSize = 32 - fieldMeta.Bits;
                        if (bitSize <= 0)
                            bitSize = columnMeta.Immediate.BitWidth;

                        return r.ReadValue64(bitSize).GetValue<T>();
                    }
                case CompressionType.Immediate:
                    {
                        return r.ReadValue64(columnMeta.Immediate.BitWidth).GetValue<T>();
                    }
                case CompressionType.Common:
                    {
                        if (commonData.TryGetValue(Id, out Value32 val))
                            return val.GetValue<T>();
                        return columnMeta.Common.DefaultValue.GetValue<T>();
                    }
                case CompressionType.Pallet:
                    {
                        uint palletIndex = r.ReadUInt32(columnMeta.Pallet.BitWidth);
                        return palletData[palletIndex].GetValue<T>();
                    }
                case CompressionType.SignedImmediate:
                    {
                        return r.ReadValue64Signed(columnMeta.Immediate.BitWidth).GetValue<T>();
                    }
                case CompressionType.PalletArray:
                    {
                        if (columnMeta.Pallet.Cardinality != 1)
                            break;

                        uint palletArrayIndex = r.ReadUInt32(columnMeta.Pallet.BitWidth);
                        return palletData[(int)palletArrayIndex].GetValue<T>();
                    }
            }

            throw new Exception(string.Format("Unexpected compression type {0}", columnMeta.CompressionType));
        }

        private static T[] GetFieldValueArray<T>(BitReader r, FieldMetaData fieldMeta, ColumnMetaData columnMeta, Value32[] palletData, Dictionary<int, Value32> commonData) where T : struct
        {
            T[] array;
            switch (columnMeta.CompressionType)
            {
                case CompressionType.None:
                    {
                        int bitSize = 32 - fieldMeta.Bits;
                        if (bitSize <= 0)
                            bitSize = columnMeta.Immediate.BitWidth;

                        array = new T[columnMeta.Size / (Unsafe.SizeOf<T>() * 8)];
                        for (int i = 0; i < array.Length; i++)
                            array[i] = r.ReadValue64(bitSize).GetValue<T>();

                        return array;
                    }

                case CompressionType.PalletArray:
                    {
                        int cardinality = columnMeta.Pallet.Cardinality;
                        uint palletArrayIndex = r.ReadUInt32(columnMeta.Pallet.BitWidth);

                        array = new T[cardinality];
                        for (int i = 0; i < array.Length; i++)
                            array[i] = palletData[i + cardinality * (int)palletArrayIndex].GetValue<T>();

                        return array;
                    }
            }

            throw new Exception(string.Format("Unexpected compression type {0}", columnMeta.CompressionType));
        }

        private static string[] GetFieldValueStringArray(BitReader r, FieldMetaData fieldMeta, ColumnMetaData columnMeta, int recordsOffset, Dictionary<long, string> stringTable)
        {
            switch (columnMeta.CompressionType)
            {
                case CompressionType.None:
                    int bitSize = 32 - fieldMeta.Bits;
                    if (bitSize <= 0)
                        bitSize = columnMeta.Immediate.BitWidth;

                    string[] array = new string[columnMeta.Size / (sizeof(int) * 8)];
                    for (int i = 0; i < array.Length; i++)
                    {
                        int offSet = recordsOffset + r.Offset + (r.Position >> 3);
                        array[i] = stringTable[offSet + r.ReadValue64(bitSize).GetValue<int>()];
                    }

                    return array;
            }
            throw new Exception(string.Format("Unexpected compression type {0}", columnMeta.CompressionType));
        }

        public IDBRow Clone()
        {
            return (IDBRow)MemberwiseClone();
        }
    }

    class WDC2Reader : BaseEncryptionSupportingReader
    {
        public uint Signature { get; }

        private const int HeaderSize = 72;
        private const uint WDC2FmtSig = 0x32434457; // WDC2
        private const uint CLS1FmtSig = 0x434C5331; // CLS1

        public WDC2Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public WDC2Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                    throw new InvalidDataException("WDC2 file is corrupted!");

                uint magic = reader.ReadUInt32();
                if (magic != WDC2FmtSig && magic != CLS1FmtSig)
                    throw new InvalidDataException("WDC2 file is corrupted!");

                Signature = magic;
                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();
                TableHash = reader.ReadUInt32();
                LayoutHash = reader.ReadUInt32();
                MinIndex = reader.ReadInt32();
                MaxIndex = reader.ReadInt32();
                Locale = reader.ReadInt32();
                Flags = (DB2Flags)reader.ReadUInt16();
                IdFieldIndex = reader.ReadUInt16();
                int totalFieldsCount = reader.ReadInt32();
                PackedDataOffset = reader.ReadInt32(); // Offset within the field where packed data starts
                int lookupColumnCount = reader.ReadInt32(); // count of lookup columns
                int columnMetaDataSize = reader.ReadInt32(); // 24 * NumFields bytes, describes column bit packing, {ushort recordOffset, ushort size, uint additionalDataSize, uint compressionType, uint packedDataOffset or commonvalue, uint cellSize, uint cardinality}[NumFields], sizeof(DBC2CommonValue) == 8
                int commonDataSize = reader.ReadInt32();
                int palletDataSize = reader.ReadInt32(); // in bytes, sizeof(DBC2PalletValue) == 4
                int sectionsCount = reader.ReadInt32();

                if (sectionsCount > 1)
                    throw new Exception("WDC2 only supports 1 section");

                if (sectionsCount == 0 || RecordsCount == 0)
                    return;

                var sections = reader.ReadArray<SectionHeader>(sectionsCount).ToList();
                this.m_sections = sections.OfType<IEncryptableDatabaseSection>().ToList();

                // field meta data
                Meta = reader.ReadArray<FieldMetaData>(FieldsCount);

                // column meta data
                ColumnMeta = reader.ReadArray<ColumnMetaData>(FieldsCount);

                // pallet data
                PalletData = new Value32[ColumnMeta.Length][];
                for (int i = 0; i < ColumnMeta.Length; i++)
                {
                    if (ColumnMeta[i].CompressionType == CompressionType.Pallet || ColumnMeta[i].CompressionType == CompressionType.PalletArray)
                    {
                        PalletData[i] = reader.ReadArray<Value32>((int)ColumnMeta[i].AdditionalDataSize / 4);
                    }
                }

                // common data
                CommonData = new Dictionary<int, Value32>[ColumnMeta.Length];
                for (int i = 0; i < ColumnMeta.Length; i++)
                {
                    if (ColumnMeta[i].CompressionType == CompressionType.Common)
                    {
                        var commonValues = new Dictionary<int, Value32>((int)ColumnMeta[i].AdditionalDataSize / 8);
                        CommonData[i] = commonValues;

                        for (int j = 0; j < ColumnMeta[i].AdditionalDataSize / 8; j++)
                            commonValues[reader.ReadInt32()] = reader.Read<Value32>();
                    }
                }

                for (int sectionIndex = 0; sectionIndex < sectionsCount; sectionIndex++)
                {
                    reader.BaseStream.Position = sections[sectionIndex].FileOffset;

                    if (!Flags.HasFlagExt(DB2Flags.Sparse))
                    {
                        // records data
                        byte[] data = reader.ReadBytes(RecordsCount * RecordSize);
                        Array.Resize(ref data, data.Length + 8); // pad with extra zeros so we don't crash when reading
                        RecordsData = data;

                        // string data
                        StringTable = new Dictionary<long, string>(sections[sectionIndex].StringTableSize / 0x20);
                        for (int i = 0; i < sections[sectionIndex].StringTableSize;)
                        {
                            long oldPos = reader.BaseStream.Position;
                            StringTable[oldPos] = reader.ReadCString();
                            i += (int)(reader.BaseStream.Position - oldPos);
                        }
                    }
                    else
                    {
                        // sparse data with inlined strings
                        RecordsData = reader.ReadBytes(sections[sectionIndex].SparseTableOffset - sections[sectionIndex].FileOffset);

                        if (reader.BaseStream.Position != sections[sectionIndex].SparseTableOffset)
                            throw new Exception("reader.BaseStream.Position != sections[sectionIndex].SparseTableOffset");

                        int sparseCount = MaxIndex - MinIndex + 1;

                        SparseEntries = new List<SparseEntry>(sparseCount);
                        CopyData = new Dictionary<int, int>(sparseCount);
                        var sparseIdLookup = new Dictionary<uint, int>(sparseCount);

                        for (int i = 0; i < sparseCount; i++)
                        {
                            SparseEntry sparse = reader.Read<SparseEntry>();
                            if (sparse.Offset == 0 || sparse.Size == 0)
                                continue;

                            if (sparseIdLookup.TryGetValue(sparse.Offset, out int copyId))
                            {
                                CopyData[MinIndex + i] = copyId;
                            }
                            else
                            {
                                SparseEntries.Add(sparse);
                                sparseIdLookup.Add(sparse.Offset, MinIndex + i);
                            }
                        }
                    }

                    // index data
                    IndexData = reader.ReadArray<int>(sections[sectionIndex].IndexDataSize / 4);

                    // fix zero-filled index data
                    if (IndexData.Length > 0 && IndexData.All(x => x == 0))
                        IndexData = Enumerable.Range(MinIndex, MaxIndex - MinIndex + 1).ToArray();

                    // duplicate rows data
                    if (CopyData == null)
                        CopyData = new Dictionary<int, int>(sections[sectionIndex].CopyTableSize / 8);

                    for (int i = 0; i < sections[sectionIndex].CopyTableSize / 8; i++)
                        CopyData[reader.ReadInt32()] = reader.ReadInt32();

                    // reference data
                    ReferenceData refData = new ReferenceData();
                    if (sections[sectionIndex].ParentLookupDataSize > 0)
                    {
                        refData.NumRecords = reader.ReadInt32();
                        refData.MinId = reader.ReadInt32();
                        refData.MaxId = reader.ReadInt32();

                        var entries = reader.ReadArray<ReferenceEntry>(refData.NumRecords);
                        for (int i = 0; i < entries.Length; i++)
                            refData.Entries[entries[i].Index] = entries[i].Id;
                    }

                    int position = 0;
                    for (int i = 0; i < RecordsCount; i++)
                    {
                        BitReader bitReader = new BitReader(RecordsData) { Position = 0 };

                        if (Flags.HasFlagExt(DB2Flags.Sparse))
                        {
                            bitReader.Position = position;
                            position += SparseEntries[i].Size * 8;
                        }
                        else
                        {
                            bitReader.Offset = i * RecordSize;
                        }

                        refData.Entries.TryGetValue(i, out int refId);

                        IDBRow rec = new WDC2Row(this, bitReader, sections[sectionIndex].FileOffset, sections[sectionIndex].IndexDataSize != 0 ? IndexData[i] : -1, refId, i);
                        _Records.Add(i, rec);
                    }
                }
            }
        }
    }
}