using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DBCD.IO.Common;

namespace DBCD.IO.Readers
{
    class WDB2Row : IDBRow
    {
        private BaseReader m_reader;
        private readonly int m_recordIndex;

        public int Id { get; set; }
        public BitReader Data { get; set; }

        public WDB2Row(BaseReader reader, BitReader data, int recordIndex)
        {
            m_reader = reader;
            Data = data;
            m_recordIndex = recordIndex + 1;

            Id = m_recordIndex = recordIndex + 1;
        }

        private static class SingleReaderCache<T>
        {
            public static readonly Dictionary<Type, Action<T, FieldCache<T>, BitReader, Dictionary<long, string>>> Readers = new()
            {
                { typeof(ulong), (record, info, data, _) => info.Setter.SetValue(record, GetFieldValue<ulong>(data)) },
                { typeof(long), (record, info, data, _) => info.Setter.SetValue(record, GetFieldValue<long>(data)) },
                { typeof(float), (record, info, data, _) => info.Setter.SetValue(record, GetFieldValue<float>(data)) },
                { typeof(uint), (record, info, data, _) => info.Setter.SetValue(record, GetFieldValue<uint>(data)) },
                { typeof(int), (record, info, data, _) => info.Setter.SetValue(record, GetFieldValue<int>(data)) },
                { typeof(ushort), (record, info, data, _) => info.Setter.SetValue(record, GetFieldValue<ushort>(data)) },
                { typeof(short), (record, info, data, _) => info.Setter.SetValue(record, GetFieldValue<short>(data)) },
                { typeof(byte), (record, info, data, _) => info.Setter.SetValue(record, GetFieldValue<byte>(data)) },
                { typeof(sbyte), (record, info, data, _) => info.Setter.SetValue(record, GetFieldValue<sbyte>(data)) },
                { typeof(string), (record, info, data, stringTable) => info.Setter.SetObject(record, stringTable[GetFieldValue<int>(data)]) }
            };
        }
        
        private static Dictionary<Type, Func<BitReader, Dictionary<long, string>, int, object>> arrayReaders = new Dictionary<Type, Func<BitReader, Dictionary<long, string>, int, object>>
        {
            [typeof(ulong[])] = (data, stringTable, cardinality) => GetFieldValueArray<ulong>(data, cardinality),
            [typeof(long[])] = (data, stringTable, cardinality) => GetFieldValueArray<long>(data, cardinality),
            [typeof(float[])] = (data, stringTable, cardinality) => GetFieldValueArray<float>(data, cardinality),
            [typeof(int[])] = (data, stringTable, cardinality) => GetFieldValueArray<int>(data, cardinality),
            [typeof(uint[])] = (data, stringTable, cardinality) => GetFieldValueArray<uint>(data, cardinality),
            [typeof(ushort[])] = (data, stringTable, cardinality) => GetFieldValueArray<ushort>(data, cardinality),
            [typeof(short[])] = (data, stringTable, cardinality) => GetFieldValueArray<short>(data, cardinality),
            [typeof(byte[])] = (data, stringTable, cardinality) => GetFieldValueArray<byte>(data, cardinality),
            [typeof(sbyte[])] = (data, stringTable, cardinality) => GetFieldValueArray<sbyte>(data, cardinality),
            [typeof(string[])] = (data, stringTable, cardinality) => GetFieldValueArray<int>(data, cardinality).Select(i => stringTable[i]).ToArray(),
        };

        public void GetFields<T>(FieldCache<T>[] fields, T entry)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                FieldCache<T> info = fields[i];
                if (info.IndexMapField)
                {
                    Id = GetFieldValue<int>(Data);
                    
                    if (info.FieldType == typeof(T))
                        info.Setter.SetValue(entry, Id);
                    else
                        info.Setter.SetObject(entry, Convert.ChangeType(Id, info.FieldType));
                    continue;
                }

                if (info.IsArray)
                {
                    if (arrayReaders.TryGetValue(info.FieldType, out var reader))
                        info.Setter.SetObject(entry, reader(Data, m_reader.StringTable, info.Cardinality));
                    else
                        throw new Exception("Unhandled array type: " + typeof(T).Name);
                }
                else if (info.IsLocalisedString)
                {
                    Data.Position += 32 * info.LocaleInfo.Locale;
                    info.Setter.SetObject(entry, m_reader.StringTable[GetFieldValue<int>(Data)]);
                    Data.Position += 32 * (info.LocaleInfo.LocaleCount - info.LocaleInfo.Locale);
                }
                else if (SingleReaderCache<T>.Readers.TryGetValue(info.FieldType, out var reader))
                    reader(entry, info, Data, m_reader.StringTable);
                else
                    throw new Exception("Unhandled field type: " + typeof(T).Name);
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

    class WDB2Reader : BaseReader
    {
        private const int HeaderSize = 28;
        private const int ExtendedHeaderSize = 48;
        private const uint WDB2FmtSig = 0x32424457; // WDB2

        public WDB2Reader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public WDB2Reader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                    throw new InvalidDataException("WDB2 file is corrupted!");

                uint magic = reader.ReadUInt32();

                if (magic != WDB2FmtSig)
                    throw new InvalidDataException("WDB2 file is corrupted!");

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();
                TableHash = reader.ReadUInt32();
                Build = reader.ReadUInt32();
                uint timestamp = reader.ReadUInt32();

                if (RecordsCount == 0)
                    return;

                // Extended header 
                if (Build > 12880)
                {
                    if (reader.BaseStream.Length < ExtendedHeaderSize)
                        throw new InvalidDataException("WDB2 file is corrupted!");

                    MinIndex = reader.ReadInt32();
                    MaxIndex = reader.ReadInt32();
                    int locale = reader.ReadInt32();
                    int copyTableSize = reader.ReadInt32();

                    if (MaxIndex > 0)
                    {
                        int diff = MaxIndex - MinIndex + 1;
                        reader.BaseStream.Position += diff * 4; // indicies uint[]
                        reader.BaseStream.Position += diff * 2; // string lengths ushort[]
                    }
                }

                byte[] data = reader.ReadBytes(RecordsCount * RecordSize);
                Array.Resize(ref data, data.Length + 8); // pad with extra zeros so we don't crash when reading
                RecordsData = data;

                for (int i = 0; i < RecordsCount; i++)
                {
                    BitReader bitReader = new BitReader(RecordsData) { Position = i * RecordSize * 8 };
                    IDBRow rec = new WDB2Row(this, bitReader, i);
                    _Records.Add(i, rec);
                }

                StringTable = reader.ReadStringTable(StringTableSize);
            }
        }
    }
}
