using DBCD.Helpers;
using DBFileReaderLib;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;

namespace DBCD
{
    public class DBCDRow : DynamicObject
    {
        public int ID;

        private readonly dynamic raw;
        private readonly FieldAccessor fieldAccessor;

        internal DBCDRow(dynamic raw, FieldAccessor fieldAccessor)
        {
            this.raw = raw;
            this.fieldAccessor = fieldAccessor;
            this.ID = raw.ID;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return fieldAccessor.TryGetMember(this.raw, binder.Name, out result);
        }
    }

    public class DynamicKeyValuePair<T>
    {
        public T Key;
        public dynamic Value;

        internal DynamicKeyValuePair(T key, dynamic value)
        {
            this.Key = key;
            this.Value = value;
        }
    }

    public interface IDBCDStorage : IEnumerable<DynamicKeyValuePair<int>>, ILookup<int, DBCDRow>
    {
        string[] AvailableColumns { get; }

        IEnumerable<dynamic> Values { get; }

        IEnumerable<int> Keys { get; }
    }

    public class DBCDStorage<T> : Storage<T>, IDBCDStorage where T : class, new()
    {
        private readonly string[] availableColumns;
        private readonly string tableName;
        private readonly FieldAccessor fieldAccessor;

        string[] IDBCDStorage.AvailableColumns => this.availableColumns;

        public DBCDStorage(Stream stream, DBCDInfo info) : this(new DBReader(stream), info) { }

        public DBCDStorage(DBReader dbReader, DBCDInfo info) : base(dbReader)
        {
            this.availableColumns = info.availableColumns;
            this.tableName = info.tableName;
            this.fieldAccessor = new FieldAccessor(typeof(T));
        }


        private IEnumerable<DBCDRow> DynamicValues => this.Values.Select(row => new DBCDRow(row, fieldAccessor));
        IEnumerable<dynamic> IDBCDStorage.Values => this.DynamicValues;
        IEnumerable<int> IDBCDStorage.Keys => this.Keys;

        IEnumerable<DBCDRow> ILookup<int, DBCDRow>.this[int key] => this.DynamicValues.Where(row => row.ID == key);

        IEnumerator<DynamicKeyValuePair<int>> IEnumerable<DynamicKeyValuePair<int>>.GetEnumerator()
        {
            return this.DynamicValues.Select(row => new DynamicKeyValuePair<int>(row.ID, row)).GetEnumerator();
        }

        public override string ToString()
        {
            return $"{this.tableName}";
        }

        public bool Contains(int key)
        {
            return this.Keys.Contains(key);
        }

        IEnumerator<IGrouping<int, DBCDRow>> IEnumerable<IGrouping<int, DBCDRow>>.GetEnumerator()
        {
            return this.DynamicValues.GroupBy(row => row.ID).GetEnumerator();
        }
    }
}