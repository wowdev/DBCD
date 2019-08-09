using DBCD.IO.Common;
using DBCD.IO.Readers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public BaseWriter(BaseReader reader)
        {
            FieldCache = typeof(T).GetFields().Select(x => new FieldCache<T>(x)).ToArray();

            FieldsCount = reader.FieldsCount;
            RecordSize = reader.RecordSize;
            IdFieldIndex = reader.IdFieldIndex;
            Flags = reader.Flags;

            m_stringsTable = new Dictionary<string, int>();
            m_copyData = new SortedDictionary<int, int>();
            m_meta = reader.Meta;
            m_columnMeta = reader.ColumnMeta;

            if (m_columnMeta != null)
            {
                m_commonData = new Dictionary<int, Value32>[m_columnMeta.Length];
                m_palletData = new List<Value32[]>[m_columnMeta.Length];
                m_referenceData = new List<int>();

                // create the lookup collections
                for (int i = 0; i < m_columnMeta.Length; i++)
                {
                    m_commonData[i] = new Dictionary<int, Value32>();
                    m_palletData[i] = new List<Value32[]>();
                }
            }

            InternString("");
        }


        #region Data

        protected FieldMetaData[] m_meta;
        public FieldMetaData[] Meta => m_meta;

        protected ColumnMetaData[] m_columnMeta;
        public ColumnMetaData[] ColumnMeta => m_columnMeta;

        protected List<Value32[]>[] m_palletData;
        public List<Value32[]>[] PalletData => m_palletData;

        protected Dictionary<int, Value32>[] m_commonData;
        public Dictionary<int, Value32>[] CommonData => m_commonData;

        protected Dictionary<string, int> m_stringsTable;
        public Dictionary<string, int> StringTable => m_stringsTable;

        protected SortedDictionary<int, int> m_copyData;
        public SortedDictionary<int, int> CopyData => m_copyData;

        protected List<int> m_referenceData;
        public List<int> ReferenceData => m_referenceData;

        #endregion

        #region Methods

        public int InternString(string value)
        {
            if (m_stringsTable.TryGetValue(value, out int index))
                return index;

            m_stringsTable.Add(value, StringTableSize);

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
                    if (m_copyData.TryGetValue(i, out int copyid))
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
