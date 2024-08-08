using DBCD.IO.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DBCD.IO.Readers
{
    abstract class BaseReader
    {
        public int RecordsCount { get; protected set; }
        public int FieldsCount { get; protected set; }
        public int RecordSize { get; protected set; }
        public int StringTableSize { get; protected set; }
        public uint TableHash { get; protected set; }
        public uint LayoutHash { get; protected set; }
        public int MinIndex { get; protected set; }
        public int MaxIndex { get; protected set; }
        public int IdFieldIndex { get; protected set; }
        public DB2Flags Flags { get; protected set; }
        public int Locale { get; protected set; }
        public uint Build { get; protected set; }
        public int PackedDataOffset { get; protected set; }

        #region Data

        public FieldMetaData[] Meta { get; protected set; }
        public int[] IndexData { get; protected set; }
        public ColumnMetaData[] ColumnMeta { get; protected set; }
        public Value32[][] PalletData { get; protected set; }
        public Dictionary<int, Value32>[] CommonData { get; protected set; }
        public Dictionary<long, string> StringTable { get; protected set; }
        public int[] ForeignKeyData { get; protected set; }

        protected Dictionary<int, int> CopyData { get; set; }
        protected byte[] RecordsData { get; set; }
        protected Dictionary<int, IDBRow> _Records { get; set; } = new Dictionary<int, IDBRow>();
        protected List<SparseEntry> SparseEntries { get; set; }

        #endregion

        #region Methods

        public void Enumerate(Action<IDBRow> action)
        {
            Parallel.ForEach(_Records.Values, action);
            Parallel.ForEach(GetCopyRows(), action);
        }

        public void Clear()
        {
            IndexData = null;
            PalletData = null;
            ColumnMeta = null;
            RecordsData = null;
            ForeignKeyData = null;
            CommonData = null;

            _Records?.Clear();
            StringTable?.Clear();
            SparseEntries?.Clear();
            CopyData?.Clear();
        }

        private IEnumerable<IDBRow> GetCopyRows()
        {
            if (CopyData == null || CopyData.Count == 0)
                yield break;

            // fix temp ids
            _Records = _Records.ToDictionary(x => x.Value.Id, x => x.Value);

            foreach (var copyRow in CopyData)
            {
                IDBRow rec = _Records[copyRow.Value].Clone();
                rec.Data = rec.Data.Clone();
                rec.Id = copyRow.Key;
                _Records[rec.Id] = rec;
                yield return rec;
            }

            CopyData.Clear();
        }

        #endregion
    }

}
