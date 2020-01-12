using DBFileReaderLib.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DBFileReaderLib.Readers
{
    class WDB6Row : IDBRow
    {
        private BitReader m_data;
        private BaseReader m_reader;
        private readonly int m_dataOffset;
        private readonly int m_dataPosition;
        private readonly int m_recordIndex;

        public int Id { get; set; }
        public BitReader Data { get => m_data; set => m_data = value; }

        private readonly FieldMetaData[] m_fieldMeta;
        private readonly Dictionary<int, Value32>[] m_commonData;

        public WDB6Row(BaseReader reader, BitReader data, int id, int recordIndex)
        {
            m_reader = reader;
            m_data = data;
            m_recordIndex = recordIndex;

            m_dataOffset = m_data.Offset;
            m_dataPosition = m_data.Position;

            m_fieldMeta = reader.Meta;
            m_commonData = reader.CommonData;

            Id = id;
        }

        private static Dictionary<Type, Func<int, BitReader, FieldMetaData, Dictionary<int, Value32>, Dictionary<long, string>, BaseReader, object>> simpleReaders = new Dictionary<Type, Func<int, BitReader, FieldMetaData, Dictionary<int, Value32>, Dictionary<long, string>, BaseReader, object>>
        {
            [typeof(long)] = (id, data, fieldMeta, commonData, stringTable, header) => GetFieldValue<long>(id, data, fieldMeta, commonData),
            [typeof(float)] = (id, data, fieldMeta, commonData, stringTable, header) => GetFieldValue<float>(id, data, fieldMeta, commonData),
            [typeof(int)] = (id, data, fieldMeta, commonData, stringTable, header) => GetFieldValue<int>(id, data, fieldMeta, commonData),
            [typeof(uint)] = (id, data, fieldMeta, commonData, stringTable, header) => GetFieldValue<uint>(id, data, fieldMeta, commonData),
            [typeof(short)] = (id, data, fieldMeta, commonData, stringTable, header) => GetFieldValue<short>(id, data, fieldMeta, commonData),
            [typeof(ushort)] = (id, data, fieldMeta, commonData, stringTable, header) => GetFieldValue<ushort>(id, data, fieldMeta, commonData),
            [typeof(sbyte)] = (id, data, fieldMeta, commonData, stringTable, header) => GetFieldValue<sbyte>(id, data, fieldMeta, commonData),
            [typeof(byte)] = (id, data, fieldMeta, commonData, stringTable, header) => GetFieldValue<byte>(id, data, fieldMeta, commonData),
            [typeof(string)] = (id, data, fieldMeta, commonData, stringTable, header) => header.Flags.HasFlagExt(DB2Flags.Sparse) ? data.ReadCString() : stringTable[GetFieldValue<int>(id, data, fieldMeta, commonData)],
        };

        private static Dictionary<Type, Func<int, BitReader, FieldMetaData, Dictionary<int, Value32>, Dictionary<long, string>, int, object>> arrayReaders = new Dictionary<Type, Func<int, BitReader, FieldMetaData, Dictionary<int, Value32>, Dictionary<long, string>, int, object>>
        {
            [typeof(ulong[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<ulong>(id, data, fieldMeta, commonData, cardinality),
            [typeof(long[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<long>(id, data, fieldMeta, commonData, cardinality),
            [typeof(float[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<float>(id, data, fieldMeta, commonData, cardinality),
            [typeof(int[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<int>(id, data, fieldMeta, commonData, cardinality),
            [typeof(uint[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<uint>(id, data, fieldMeta, commonData, cardinality),
            [typeof(ulong[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<ulong>(id, data, fieldMeta, commonData, cardinality),
            [typeof(ushort[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<ushort>(id, data, fieldMeta, commonData, cardinality),
            [typeof(short[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<short>(id, data, fieldMeta, commonData, cardinality),
            [typeof(byte[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<byte>(id, data, fieldMeta, commonData, cardinality),
            [typeof(sbyte[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<sbyte>(id, data, fieldMeta, commonData, cardinality),
            [typeof(string[])] = (id, data, fieldMeta, commonData, stringTable, cardinality) => GetFieldValueArray<int>(id, data, fieldMeta, commonData, cardinality).Select(i => stringTable[i]).ToArray(),
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
                        indexFieldOffSet++;
                    else
                        Id = GetFieldValue<int>(0, m_data, m_fieldMeta[i], m_commonData?[i]);

                    info.Setter(entry, Convert.ChangeType(Id, info.FieldType));
                    continue;
                }

                object value = null;
                int fieldIndex = i - indexFieldOffSet;

                // 0x2 SecondaryKey
                if (fieldIndex >= m_reader.Meta.Length)
                {
                    info.Setter(entry, Convert.ChangeType(m_reader.ForeignKeyData[Id - m_reader.MinIndex], info.FieldType));
                    continue;
                }

                if (info.IsArray)
                {
                    if (info.Cardinality <= 1)
                        SetCardinality(info, fieldIndex);

                    if (arrayReaders.TryGetValue(info.FieldType, out var reader))
                        value = reader(Id, m_data, m_fieldMeta[fieldIndex], m_commonData?[fieldIndex], m_reader.StringTable, info.Cardinality);
                    else
                        throw new Exception("Unhandled array type: " + typeof(T).Name);
                }
                else
                {
                    if (simpleReaders.TryGetValue(info.FieldType, out var reader))
                        value = reader(Id, m_data, m_fieldMeta[fieldIndex], m_commonData?[fieldIndex], m_reader.StringTable, m_reader);
                    else
                        throw new Exception("Unhandled field type: " + typeof(T).Name);
                }

                info.Setter(entry, value);
            }
        }

        /// <summary>
        /// Cardinality can be calculated from the file itself
        /// - Last field of the record : (header.RecordSize - current offset) / sizeof(ValueType)
        /// - Middle field : (next field offset - current offset) / sizeof(ValueType)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="info"></param>
        /// <param name="fieldIndex"></param>
        private void SetCardinality<T>(FieldCache<T> info, int fieldIndex)
        {
            int fieldOffset = m_fieldMeta[fieldIndex].Offset;
            int fieldValueSize = (32 - m_fieldMeta[fieldIndex].Bits) >> 3;

            int nextOffset;
            if (fieldIndex + 1 >= m_fieldMeta.Length)
                nextOffset = m_reader.RecordSize; // get total record size
            else
                nextOffset = m_fieldMeta[fieldIndex + 1].Offset; // get next field offset

            info.Cardinality = (nextOffset - fieldOffset) / fieldValueSize;
        }

        private static T GetFieldValue<T>(int Id, BitReader r, FieldMetaData fieldMeta, Dictionary<int, Value32> commonData) where T : struct
        {
            if (commonData?.TryGetValue(Id, out var value) == true)
                return value.GetValue<T>();

            return r.ReadValue64(32 - fieldMeta.Bits).GetValue<T>();
        }

        private static T[] GetFieldValueArray<T>(int Id, BitReader r, FieldMetaData fieldMeta, Dictionary<int, Value32> commonData, int cardinality) where T : struct
        {
            T[] array = new T[cardinality];
            for (int i = 0; i < array.Length; i++)
            {
                if (commonData?.TryGetValue(Id, out var value) == true)
                    array[1] = value.GetValue<T>();
                else
                    array[i] = r.ReadValue64(32 - fieldMeta.Bits).GetValue<T>();
            }

            return array;
        }

        public IDBRow Clone()
        {
            return (IDBRow)MemberwiseClone();
        }
    }

    class WDB6Reader : BaseReader
    {
        private const int HeaderSize = 56;
        private const uint WDB6FmtSig = 0x36424457; // WDB6

        // CommonData type enum to bit size
        private readonly Dictionary<byte, short> CommonTypeBits = new Dictionary<byte, short>
        {
            { 0, 0 },  // string
            { 1, 16 }, // short
            { 2, 24 }, // byte
            { 3, 0 },  // float
            { 4, 0 },  // int
        };

        public WDB6Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public unsafe WDB6Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                    throw new InvalidDataException("WDB6 file is corrupted!");

                uint magic = reader.ReadUInt32();

                if (magic != WDB6FmtSig)
                    throw new InvalidDataException("WDB6 file is corrupted!");

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();
                TableHash = reader.ReadUInt32();
                LayoutHash = reader.ReadUInt32();
                MinIndex = reader.ReadInt32();
                MaxIndex = reader.ReadInt32();
                int locale = reader.ReadInt32();
                int copyTableSize = reader.ReadInt32();
                Flags = (DB2Flags)reader.ReadUInt16();
                IdFieldIndex = reader.ReadUInt16();
                int totalFieldCount = reader.ReadInt32();
                int commonDataSize = reader.ReadInt32();

                if (RecordsCount == 0)
                    return;

                // field meta data
                m_meta = reader.ReadArray<FieldMetaData>(FieldsCount);

                if (!Flags.HasFlagExt(DB2Flags.Sparse))
                {
                    // records data
                    recordsData = reader.ReadBytes(RecordsCount * RecordSize);
                    Array.Resize(ref recordsData, recordsData.Length + 8); // pad with extra zeros so we don't crash when reading

                    // string table
                    m_stringsTable = new Dictionary<long, string>(StringTableSize / 0x20);
                    for (int i = 0; i < StringTableSize;)
                    {
                        long oldPos = reader.BaseStream.Position;
                        m_stringsTable[i] = reader.ReadCString();
                        i += (int)(reader.BaseStream.Position - oldPos);
                    }
                }
                else
                {
                    // sparse data with inlined strings
                    recordsData = reader.ReadBytes(StringTableSize - (int)reader.BaseStream.Position);

                    int sparseCount = MaxIndex - MinIndex + 1;

                    m_sparseEntries = new List<SparseEntry>(sparseCount);
                    m_copyData = new Dictionary<int, int>(sparseCount);
                    var sparseIdLookup = new Dictionary<uint, int>(sparseCount);

                    for (int i = 0; i < sparseCount; i++)
                    {
                        SparseEntry sparse = reader.Read<SparseEntry>();
                        if (sparse.Offset == 0 || sparse.Size == 0)
                            continue;

                        if (sparseIdLookup.TryGetValue(sparse.Offset, out int copyId))
                        {
                            m_copyData[MinIndex + i] = copyId;
                        }
                        else
                        {
                            m_sparseEntries.Add(sparse);
                            sparseIdLookup.Add(sparse.Offset, MinIndex + i);
                        }
                    }
                }

                // secondary key
                if (Flags.HasFlagExt(DB2Flags.SecondaryKey))
                    m_foreignKeyData = reader.ReadArray<int>(MaxIndex - MinIndex + 1);

                // index table
                if (Flags.HasFlagExt(DB2Flags.Index))
                    m_indexData = reader.ReadArray<int>(RecordsCount);

                // duplicate rows data
                if (m_copyData == null)
                    m_copyData = new Dictionary<int, int>(copyTableSize / 8);

                for (int i = 0; i < copyTableSize / 8; i++)
                    m_copyData[reader.ReadInt32()] = reader.ReadInt32();

                if (commonDataSize > 0)
                {
                    Array.Resize(ref m_meta, totalFieldCount);

                    int fieldCount = reader.ReadInt32();
                    m_commonData = new Dictionary<int, Value32>[fieldCount];

                    // HACK as of 24473 values are 4 byte aligned
                    // try to calculate this by seeing if all <id, value> tuples are 8 bytes
                    bool aligned = (commonDataSize - 4 - (fieldCount * 5)) % 8 == 0;

                    for (int i = 0; i < fieldCount; i++)
                    {
                        int count = reader.ReadInt32();
                        byte type = reader.ReadByte();
                        int size = aligned ? 4 : (32 - CommonTypeBits[type]) >> 3;

                        // add the new meta entry
                        if (i > FieldsCount)
                        {
                            m_meta[i] = new FieldMetaData()
                            {
                                Bits = CommonTypeBits[type],
                                Offset = (short)(m_meta[i - 1].Offset + ((32 - m_meta[i - 1].Bits) >> 3))
                            };
                        }

                        var commonValues = new Dictionary<int, Value32>(count);
                        for (int j = 0; j < count; j++)
                        {
                            int id = reader.ReadInt32();
                            byte[] buffer = reader.ReadBytes(size);
                            Value32 value = Unsafe.ReadUnaligned<Value32>(ref buffer[0]);

                            commonValues.Add(id, value);
                        }

                        m_commonData[i] = commonValues;
                    }
                }

                int position = 0;
                for (int i = 0; i < RecordsCount; i++)
                {
                    BitReader bitReader = new BitReader(recordsData) { Position = 0 };

                    if (Flags.HasFlagExt(DB2Flags.Sparse))
                    {
                        bitReader.Position = position;
                        position += m_sparseEntries[i].Size * 8;
                    }
                    else
                    {
                        bitReader.Offset = i * RecordSize;
                    }

                    IDBRow rec = new WDB6Row(this, bitReader, Flags.HasFlagExt(DB2Flags.Index) ? m_indexData[i] : -1, i);
                    _Records.Add(i, rec);
                }
            }
        }
    }

}
