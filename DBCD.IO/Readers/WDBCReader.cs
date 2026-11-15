using DBCD.IO.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DBCD.IO.Readers
{
    class WDBCRow : IDBRow
    {
        private BaseReader m_reader;
        private readonly int m_recordIndex;

        public int Id { get; set; }
        public BitReader Data { get; set; }

        public WDBCRow(BaseReader reader, BitReader data, int recordIndex)
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
                { typeof(long), (entry, info, data, _) => info.Setter.SetValue(entry, GetFieldValue<long>(data)) },
                { typeof(ulong), (entry, info, data, _) => info.Setter.SetValue(entry, GetFieldValue<ulong>(data)) },
                { typeof(float), (entry, info, data, _) => info.Setter.SetValue(entry, GetFieldValue<float>(data)) },
                { typeof(uint), (entry, info, data, _) => info.Setter.SetValue(entry, GetFieldValue<uint>(data)) },
                { typeof(int), (entry, info, data, _) => info.Setter.SetValue(entry, GetFieldValue<int>(data)) },
                { typeof(ushort), (entry, info, data, _) => info.Setter.SetValue(entry, GetFieldValue<ushort>(data)) },
                { typeof(short), (entry, info, data, _) => info.Setter.SetValue(entry, GetFieldValue<short>(data)) },
                { typeof(byte), (entry, info, data, _) => info.Setter.SetValue(entry, GetFieldValue<byte>(data)) },
                { typeof(sbyte), (entry, info, data, _) => info.Setter.SetValue(entry, GetFieldValue<sbyte>(data)) },
                { typeof(string), (entry, info, data, stringTable) => info.Setter.SetObject(entry, stringTable[GetFieldValue<int>(data)]) }
            };
        }
        
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
            for (int i = 0; i < fields.Length; i++)
            {
                FieldCache<T> info = fields[i];
                if (info.IndexMapField)
                {
                    Id = GetFieldValue<int>(Data);
                    if (info.FieldType == typeof(int))
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
                    reader.Invoke(entry, info, Data, m_reader.StringTable);
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

    class WDBCReader : BaseReader
    {
        private const int HeaderSize = 20;
        private const uint WDBCFmtSig = 0x43424457; // WDBC

        public WDBCReader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public WDBCReader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                    throw new InvalidDataException("WDBC file is corrupted!");

                uint magic = reader.ReadUInt32();

                if (magic != WDBCFmtSig)
                    throw new InvalidDataException("WDBC file is corrupted!");

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();

                if (RecordsCount == 0)
                    return;

                byte[] data = reader.ReadBytes(RecordsCount * RecordSize);
                Array.Resize(ref data, data.Length + 8); // pad with extra zeros so we don't crash when reading
                RecordsData = data;

                for (int i = 0; i < RecordsCount; i++)
                {
                    BitReader bitReader = new BitReader(RecordsData) { Position = i * RecordSize * 8 };
                    IDBRow rec = new WDBCRow(this, bitReader, i);
                    _Records.Add(i, rec);
                }

                StringTable = reader.ReadStringTable(StringTableSize);
            }
        }
    }
}
