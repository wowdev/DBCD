using DBCD.IO.Common;
using DBCD.IO.Readers;
using System.Collections.Generic;
using System.IO;

namespace DBCD.IO.Writers
{
    abstract class BaseWriter<T> where T : class
    {
        public FieldCache<T>[] FieldCache { get; protected set; }
        public int RecordsCount { get; protected set; }
        public int StringTableSize { get; protected set; }
        public int FieldsCount { get; }
        public int RecordSize { get; }
        public int IdFieldIndex { get; }
        public DB2Flags Flags { get; }

        #region Data

        public FieldMetaData[] Meta { get; protected set; }
        public ColumnMetaData[] ColumnMeta { get; protected set; }
        public List<Value32[]>[] PalletData { get; protected set; }
        public Dictionary<int, Value32>[] CommonData { get; protected set; }
        public Dictionary<string, int> StringTable { get; protected set; }
        public SortedDictionary<int, int> CopyData { get; protected set; }
        public List<int> ReferenceData { get; protected set; }

        #endregion

        public BaseWriter(BaseReader reader)
        {
            FieldCache = typeof(T).ToFieldCache<T>();

            FieldsCount = reader.FieldsCount;
            RecordSize = reader.RecordSize;
            IdFieldIndex = reader.IdFieldIndex;
            Flags = reader.Flags;

            StringTable = new Dictionary<string, int>();
            CopyData = new SortedDictionary<int, int>();
            Meta = reader.Meta;
            ColumnMeta = reader.ColumnMeta;

            if (ColumnMeta != null)
            {
                CommonData = new Dictionary<int, Value32>[ColumnMeta.Length];
                PalletData = new List<Value32[]>[ColumnMeta.Length];
                ReferenceData = new List<int>();

                // create the lookup collections
                for (int i = 0; i < ColumnMeta.Length; i++)
                {
                    CommonData[i] = new Dictionary<int, Value32>();
                    PalletData[i] = new List<Value32[]>();
                }
            }

            // add an empty string at the first index
            InternString("");
        }            

        #region Methods

        public int InternString(string value)
        {
            if (StringTable.TryGetValue(value, out int index))
                return index;

            StringTable.Add(value, StringTableSize);

            int offset = StringTableSize;
            StringTableSize += value.Length + 1;
            return offset;
        }

        public void WriteOffsetRecords(BinaryWriter writer, IDBRowSerializer<T> serializer, uint recordOffset, int sparseCount)
        {
            var sparseIdLookup = new Dictionary<int, uint>(sparseCount);

            for (int i = 0; i < sparseCount; i++)
            {
                if (serializer.Records.TryGetValue(i, out var record))
                {
                    if (CopyData.TryGetValue(i, out int copyid))
                    {
                        // copy records use their parent's offset
                        writer.Write(sparseIdLookup[copyid]);
                        writer.Write(record.TotalBytesWrittenOut);
                    }
                    else
                    {
                        writer.Write(sparseIdLookup[i] = recordOffset);
                        writer.Write(record.TotalBytesWrittenOut);
                        recordOffset += (uint)record.TotalBytesWrittenOut;
                    }
                }
                else
                {
                    // unused ids are empty records
                    writer.BaseStream.Position += 6;
                }
            }
        }

        public void WriteSecondaryKeyData(BinaryWriter writer, IDictionary<int, T> storage, int sparseCount)
        {
            // this was always the final field of wmominimaptexture.db2
            var fieldInfo = FieldCache[FieldCache.Length - 1];
            for (int i = 0; i < sparseCount; i++)
            {
                if (storage.TryGetValue(i, out var record))
                    writer.Write((int)fieldInfo.Getter(record));
                else
                    writer.BaseStream.Position += 4;
            }
        }

        #endregion
    }
}
