﻿using DBCD.IO.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DBCD.IO.Readers
{
    class WDB5Row : IDBRow
    {
        private BaseReader m_reader;
        private readonly int m_dataOffset;
        private readonly int m_dataPosition;
        private readonly int m_recordIndex;

        public int Id { get; set; }
        public BitReader Data { get; set; }

        private readonly FieldMetaData[] m_fieldMeta;

        public WDB5Row(BaseReader reader, BitReader data, int id, int recordIndex)
        {
            m_reader = reader;
            Data = data;
            m_recordIndex = recordIndex;

            Id = id;

            m_dataOffset = Data.Offset;
            m_dataPosition = Data.Position;
            m_fieldMeta = reader.Meta;
        }

        private static Dictionary<Type, Func<BitReader, FieldMetaData, Dictionary<long, string>, BaseReader, object>> simpleReaders = new Dictionary<Type, Func<BitReader, FieldMetaData, Dictionary<long, string>, BaseReader, object>>
        {
            [typeof(long)] = (data, fieldMeta, stringTable, header) => GetFieldValue<long>(data, fieldMeta),
            [typeof(float)] = (data, fieldMeta, stringTable, header) => GetFieldValue<float>(data, fieldMeta),
            [typeof(int)] = (data, fieldMeta, stringTable, header) => GetFieldValue<int>(data, fieldMeta),
            [typeof(uint)] = (data, fieldMeta, stringTable, header) => GetFieldValue<uint>(data, fieldMeta),
            [typeof(short)] = (data, fieldMeta, stringTable, header) => GetFieldValue<short>(data, fieldMeta),
            [typeof(ushort)] = (data, fieldMeta, stringTable, header) => GetFieldValue<ushort>(data, fieldMeta),
            [typeof(sbyte)] = (data, fieldMeta, stringTable, header) => GetFieldValue<sbyte>(data, fieldMeta),
            [typeof(byte)] = (data, fieldMeta, stringTable, header) => GetFieldValue<byte>(data, fieldMeta),
            [typeof(string)] = (data, fieldMeta, stringTable, header) => header.Flags.HasFlagExt(DB2Flags.Sparse) ? data.ReadCString() : stringTable[GetFieldValue<int>(data, fieldMeta)],
        };

        private static Dictionary<Type, Func<BitReader, FieldMetaData, Dictionary<long, string>, int, object>> arrayReaders = new Dictionary<Type, Func<BitReader, FieldMetaData, Dictionary<long, string>, int, object>>
        {
            [typeof(ulong[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<ulong>(data, fieldMeta, cardinality),
            [typeof(long[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<long>(data, fieldMeta, cardinality),
            [typeof(float[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<float>(data, fieldMeta, cardinality),
            [typeof(int[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<int>(data, fieldMeta, cardinality),
            [typeof(uint[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<uint>(data, fieldMeta, cardinality),
            [typeof(ulong[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<ulong>(data, fieldMeta, cardinality),
            [typeof(ushort[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<ushort>(data, fieldMeta, cardinality),
            [typeof(short[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<short>(data, fieldMeta, cardinality),
            [typeof(byte[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<byte>(data, fieldMeta, cardinality),
            [typeof(sbyte[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<sbyte>(data, fieldMeta, cardinality),
            [typeof(string[])] = (data, fieldMeta, stringTable, cardinality) => GetFieldValueArray<int>(data, fieldMeta, cardinality).Select(i => stringTable[i]).ToArray(),
        };

        public void GetFields<T>(FieldCache<T>[] fields, T entry)
        {
            int indexFieldOffSet = 0;

            Data.Position = m_dataPosition;
            Data.Offset = m_dataOffset;

            for (int i = 0; i < fields.Length; i++)
            {
                FieldCache<T> info = fields[i];

                /* 
                 * Note: While WDB5 was introduced in build 21479, idFieldIndex wasn't added to it until build 21737.
                 * This means that the check below here will likely fail for the ~6 builds between those.
                 */
                if (i == m_reader.IdFieldIndex)
                {
                    if (Id != -1)
                        indexFieldOffSet++;
                    else
                        Id = GetFieldValue<int>(Data, m_fieldMeta[i]);

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
                        value = reader(Data, m_fieldMeta[fieldIndex], m_reader.StringTable, info.Cardinality);
                    else
                        throw new Exception("Unhandled array type: " + typeof(T).Name);
                }
                else
                {
                    if (simpleReaders.TryGetValue(info.FieldType, out var reader))
                        value = reader(Data, m_fieldMeta[fieldIndex], m_reader.StringTable, m_reader);
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

        private static T GetFieldValue<T>(BitReader r, FieldMetaData fieldMeta) where T : struct
        {
            return r.ReadValue64(32 - fieldMeta.Bits).GetValue<T>();
        }

        private static T[] GetFieldValueArray<T>(BitReader r, FieldMetaData fieldMeta, int cardinality) where T : struct
        {
            T[] array = new T[cardinality];
            for (int i = 0; i < array.Length; i++)
                array[i] = r.ReadValue64(32 - fieldMeta.Bits).GetValue<T>();

            return array;
        }

        public IDBRow Clone()
        {
            return (IDBRow)MemberwiseClone();
        }
    }

    class WDB5Reader : BaseReader
    {
        private const int HeaderSize = 52;
        private const uint WDB5FmtSig = 0x35424457; // WDB5

        public WDB5Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public WDB5Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                    throw new InvalidDataException("WDB5 file is corrupted!");

                uint magic = reader.ReadUInt32();

                if (magic != WDB5FmtSig)
                    throw new InvalidDataException("WDB5 file is corrupted!");

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();
                TableHash = reader.ReadUInt32();
                LayoutHash = reader.ReadUInt32();
                MinIndex = reader.ReadInt32();
                MaxIndex = reader.ReadInt32();
                Locale = reader.ReadInt32();
                int copyTableSize = reader.ReadInt32();
                Flags = (DB2Flags)reader.ReadUInt16();
                IdFieldIndex = reader.ReadUInt16(); // Only in build 21737+, what happens in the ~6 builds between 21479 and 21737?

                // field meta data
                Meta = reader.ReadArray<FieldMetaData>(FieldsCount);

                if (RecordsCount == 0)
                    return;

                if (!Flags.HasFlagExt(DB2Flags.Sparse))
                {
                    // records data
                    byte[] data = reader.ReadBytes(RecordsCount * RecordSize);
                    Array.Resize(ref data, data.Length + 8); // pad with extra zeros so we don't crash when reading
                    RecordsData = data;

                    // string table
                    StringTable = reader.ReadStringTable(StringTableSize);
                }
                else
                {
                    // sparse data with inlined strings
                    RecordsData = reader.ReadBytes(StringTableSize - (int)reader.BaseStream.Position);

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

                // secondary key
                if (Flags.HasFlagExt(DB2Flags.SecondaryKey))
                    ForeignKeyData = reader.ReadArray<int>(MaxIndex - MinIndex + 1);

                // index table
                if (Flags.HasFlagExt(DB2Flags.Index))
                    IndexData = reader.ReadArray<int>(RecordsCount);

                // duplicate rows data
                if (CopyData == null)
                    CopyData = new Dictionary<int, int>(copyTableSize / 8);

                for (int i = 0; i < copyTableSize / 8; i++)
                    CopyData[reader.ReadInt32()] = reader.ReadInt32();

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

                    IDBRow rec = new WDB5Row(this, bitReader, Flags.HasFlagExt(DB2Flags.Index) ? IndexData[i] : -1, i);
                    _Records.Add(i, rec);
                }
            }
        }
    }
}
