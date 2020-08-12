using DBFileReaderLib.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DBFileReaderLib.Readers
{
    class WDB3Row : IDBRow
    {
        private BitReader m_data;
        private BaseReader m_reader;
        private readonly int m_dataOffset;
        private readonly int m_dataPosition;
        private readonly int m_recordIndex;

        public int Id { get; set; }
        public BitReader Data { get => m_data; set => m_data = value; }

        public WDB3Row(BaseReader reader, BitReader data, int id, int recordIndex)
        {
            m_reader = reader;
            m_data = data;
            m_recordIndex = recordIndex;

            Id = id;

            m_dataOffset = m_data.Offset;
            m_dataPosition = m_data.Position;
        }

        private static Dictionary<Type, Func<BitReader, Dictionary<long, string>, BaseReader, object>> simpleReaders = new Dictionary<Type, Func<BitReader, Dictionary<long, string>, BaseReader, object>>
        {
            [typeof(long)] = (data, stringTable, header) => GetFieldValue<long>(data),
            [typeof(float)] = (data, stringTable, header) => GetFieldValue<float>(data),
            [typeof(int)] = (data, stringTable, header) => GetFieldValue<int>(data),
            [typeof(uint)] = (data, stringTable, header) => GetFieldValue<uint>(data),
            [typeof(short)] = (data, stringTable, header) => GetFieldValue<short>(data),
            [typeof(ushort)] = (data, stringTable, header) => GetFieldValue<ushort>(data),
            [typeof(sbyte)] = (data, stringTable, header) => GetFieldValue<sbyte>(data),
            [typeof(byte)] = (data, stringTable, header) => GetFieldValue<byte>(data),
            [typeof(string)] = (data, stringTable, header) => header.Flags.HasFlagExt(DB2Flags.Sparse) ? data.ReadCString() : stringTable[GetFieldValue<int>(data)],
        };

        private static Dictionary<Type, Func<BitReader, Dictionary<long, string>, int, object>> arrayReaders = new Dictionary<Type, Func<BitReader, Dictionary<long, string>, int, object>>
        {
            [typeof(ulong[])] = (data, stringTable, cardinality) => GetFieldValueArray<ulong>(data, cardinality),
            [typeof(long[])] = (data, stringTable, cardinality) => GetFieldValueArray<long>(data, cardinality),
            [typeof(float[])] = (data, stringTable, cardinality) => GetFieldValueArray<float>(data, cardinality),
            [typeof(int[])] = (data, stringTable, cardinality) => GetFieldValueArray<int>(data, cardinality),
            [typeof(uint[])] = (data, stringTable, cardinality) => GetFieldValueArray<uint>(data, cardinality),
            [typeof(ulong[])] = (data, stringTable, cardinality) => GetFieldValueArray<ulong>(data, cardinality),
            [typeof(ushort[])] = (data, stringTable, cardinality) => GetFieldValueArray<ushort>(data, cardinality),
            [typeof(short[])] = (data, stringTable, cardinality) => GetFieldValueArray<short>(data, cardinality),
            [typeof(byte[])] = (data, stringTable, cardinality) => GetFieldValueArray<byte>(data, cardinality),
            [typeof(sbyte[])] = (data, stringTable, cardinality) => GetFieldValueArray<sbyte>(data, cardinality),
            [typeof(string[])] = (data, stringTable, cardinality) => GetFieldValueArray<int>(data, cardinality).Select(i => stringTable[i]).ToArray(),
        };

        public void GetFields<T>(FieldCache<T>[] fields, T entry)
        {
            int indexFieldOffSet = 0;

            m_data.Position = m_dataPosition;
            m_data.Offset = m_dataOffset;

            for (int i = 0; i < fields.Length; i++)
            {
                FieldCache<T> info = fields[i];
                if (fields[i].IndexMapField)
                {
                    if (Id != -1)
                        indexFieldOffSet++;
                    else
                        Id = GetFieldValue<int>(m_data);

                    info.Setter(entry, Convert.ChangeType(Id, info.FieldType));
                    continue;
                }

                object value = null;
                int fieldIndex = i - indexFieldOffSet;

                // 0x2 SecondaryKey
                if (fieldIndex >= m_reader.FieldsCount)
                {
                    info.Setter(entry, Convert.ChangeType(m_reader.ForeignKeyData[Id - m_reader.MinIndex], info.FieldType));
                    continue;
                }

                if (info.IsArray)
                {
                    if (arrayReaders.TryGetValue(info.FieldType, out var reader))
                        value = reader(m_data, m_reader.StringTable, info.Cardinality);
                    else
                        throw new Exception("Unhandled array type: " + typeof(T).Name);
                }
                else
                {
                    if (simpleReaders.TryGetValue(info.FieldType, out var reader))
                        value = reader(m_data, m_reader.StringTable, m_reader);
                    else
                        throw new Exception("Unhandled field type: " + typeof(T).Name);
                }

                info.Setter(entry, value);
            }
        }

        private static T GetFieldValue<T>(BitReader r) where T : struct
        {
            return r.ReadValue64(Unsafe.SizeOf<T>() * 8).GetValue<T>();
        }

        private static T[] GetFieldValueArray<T>(BitReader r, int cardinality) where T : struct
        {
            T[] array = new T[cardinality];
            for (int i = 0; i < array.Length; i++)
                array[i] = r.ReadValue64(Unsafe.SizeOf<T>() * 8).GetValue<T>();

            return array;
        }

        public IDBRow Clone()
        {
            return (IDBRow)MemberwiseClone();
        }
    }

    class WDB3Reader : BaseReader
    {
        private const int HeaderSize = 48;
        private const uint WDB3FmtSig = 0x33424457; // WDB3

        // flags were added inline in WDB4, these are from meta
        // not worth documenting Index as this can be calculated
        private readonly Dictionary<uint, DB2Flags> FlagTable = new Dictionary<uint, DB2Flags>
        {
            { 3348406326u, DB2Flags.Sparse }, // conversationline
            { 2442913102u, DB2Flags.Sparse }, // item-sparse
            { 2982519032u, DB2Flags.Sparse | DB2Flags.SecondaryKey }, // wmominimaptexture
        };

        public WDB3Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public WDB3Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                    throw new InvalidDataException("WDB3 file is corrupted!");

                uint magic = reader.ReadUInt32();

                if (magic != WDB3FmtSig)
                    throw new InvalidDataException("WDB3 file is corrupted!");

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();
                TableHash = reader.ReadUInt32();
                uint build = reader.ReadUInt32();
                uint timestamp = reader.ReadUInt32();
                MinIndex = reader.ReadInt32();
                MaxIndex = reader.ReadInt32();
                int locale = reader.ReadInt32();
                int copyTableSize = reader.ReadInt32();

                if (RecordsCount == 0)
                    return;

                // apply known flags
                if (FlagTable.TryGetValue(TableHash, out var flags))
                    Flags |= flags;

                // sparse data with inlined strings
                if (Flags.HasFlagExt(DB2Flags.Sparse))
                {
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

                    // secondary key
                    if (Flags.HasFlagExt(DB2Flags.SecondaryKey))
                        m_foreignKeyData = reader.ReadArray<int>(MaxIndex - MinIndex + 1);

                    recordsData = reader.ReadBytes(m_sparseEntries.Sum(x => x.Size));
                }
                else
                {
                    // secondary key
                    if (Flags.HasFlagExt(DB2Flags.SecondaryKey))
                        m_foreignKeyData = reader.ReadArray<int>(MaxIndex - MinIndex + 1);

                    // record data
                    recordsData = reader.ReadBytes(RecordsCount * RecordSize);
                    Array.Resize(ref recordsData, recordsData.Length + 8); // pad with extra zeros so we don't crash when reading
                }

                // string table
                m_stringsTable = new Dictionary<long, string>(StringTableSize / 0x20);
                for (int i = 0; i < StringTableSize;)
                {
                    long oldPos = reader.BaseStream.Position;
                    m_stringsTable[i] = reader.ReadCString();
                    i += (int)(reader.BaseStream.Position - oldPos);
                }

                // index table
                if ((reader.BaseStream.Position + copyTableSize) < reader.BaseStream.Length)
                {
                    m_indexData = reader.ReadArray<int>(RecordsCount);
                    Flags |= DB2Flags.Index;
                }

                // duplicate rows data
                if (m_copyData == null)
                    m_copyData = new Dictionary<int, int>(copyTableSize / 8);

                for (int i = 0; i < copyTableSize / 8; i++)
                    m_copyData[reader.ReadInt32()] = reader.ReadInt32();

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

                    IDBRow rec = new WDB3Row(this, bitReader, Flags.HasFlagExt(DB2Flags.Index) ? m_indexData[i] : -1, i);
                    _Records.Add(i, rec);
                }
            }
        }
    }
}
