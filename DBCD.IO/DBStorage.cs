using DBCD.IO.Readers;
using DBCD.IO.Writers;
using System;
using System.Collections.Generic;
using System.IO;

namespace DBCD.IO
{
    public class DBStorage<T> : SortedDictionary<int, T> where T : class, new()
    {
        private readonly DBParser _reader;

        #region Header

        public string Identifier => _reader.Identifier;
        public int RecordsCount => _reader.RecordsCount;
        public int FieldsCount => _reader.FieldsCount;
        public int RecordSize => _reader.RecordSize;
        public uint TableHash => _reader.TableHash;
        public uint LayoutHash => _reader.LayoutHash;
        public int IdFieldIndex => _reader.IdFieldIndex;
        public DB2Flags Flags => _reader.Flags;
        public int Locale => _reader.Locale;

        #endregion

        #region Constructors

        public DBStorage(string fileName) : this(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) { }

        public DBStorage(Stream stream) : this(new DBParser(stream)) => _reader.ClearCache();

        public DBStorage(DBParser dbReader)
        {
            _reader = dbReader;
            _reader.ReadRecords(this);
        }

        #endregion

        #region Methods

        public void Save(string fileName) => _reader.WriteRecords(this, fileName);

        public void Save(Stream stream) => _reader.WriteRecords(this, stream);

        #endregion
    }
}
