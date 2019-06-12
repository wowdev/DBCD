using DBCD.Helpers;
using DBFileReaderLib;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        internal DBCDRow(int ID, dynamic raw, FieldAccessor fieldAccessor)
        {
            this.raw = raw;
            this.fieldAccessor = fieldAccessor;
            this.ID = ID;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return fieldAccessor.TryGetMember(this.raw, binder.Name, out result);
        }

        public object this[string fieldName]
        {
            get => fieldAccessor[this.raw, fieldName];
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

    public interface IDBCDStorage : IEnumerable<DynamicKeyValuePair<int>>, IDictionary<int, DBCDRow>
    {
        string[] AvailableColumns { get; }

        /// <summary>
        /// A readonly representation of the data as IDictionary&lt;int, <typeparamref name="T"/>&gt;
        /// </summary>
        IDictionary BackingCollection { get; }
    }

    public class DBCDStorage<T> : ReadOnlyDictionary<int, DBCDRow>, IDBCDStorage where T : class, new()
    {
        private readonly string[] availableColumns;
        private readonly string tableName;
        private readonly FieldAccessor fieldAccessor;
        private readonly ReadOnlyDictionary<int, T> storage;

        string[] IDBCDStorage.AvailableColumns => this.availableColumns;

        public DBCDStorage(Stream stream, DBCDInfo info) : this(new DBReader(stream), info) { }

        public DBCDStorage(DBReader dbReader, DBCDInfo info) : base(new Dictionary<int, DBCDRow>())
        {
            this.availableColumns = info.availableColumns;
            this.tableName = info.tableName;
            this.fieldAccessor = new FieldAccessor(typeof(T));

            // populate the collection so we don't iterate all values and create new rows each time
            storage = new ReadOnlyDictionary<int, T>(dbReader.GetRecords<T>());
            foreach (var record in storage)
                base.Dictionary.Add(record.Key, new DBCDRow(record.Key, record.Value, fieldAccessor));
        }

        IEnumerator<DynamicKeyValuePair<int>> IEnumerable<DynamicKeyValuePair<int>>.GetEnumerator()
        {
            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
                yield return new DynamicKeyValuePair<int>(enumerator.Current.Key, enumerator.Current.Value);
        }
        
        IDictionary IDBCDStorage.BackingCollection => new ReadOnlyDictionary<int, T>(storage);

        public override string ToString() => $"{this.tableName}";

    }
}