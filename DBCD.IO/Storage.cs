using System.Collections.Generic;
using System.IO;

namespace DBCD.IO
{
    public class Storage<T> : SortedDictionary<int, T> where T : class, new()
    {
        private readonly DBParser parser;

        #region Header

        public string Identifier => parser.Identifier;
        public int RecordsCount => parser.RecordsCount;
        public int FieldsCount => parser.FieldsCount;
        public int RecordSize => parser.RecordSize;
        public uint TableHash => parser.TableHash;
        public uint LayoutHash => parser.LayoutHash;
        public int IdFieldIndex => parser.IdFieldIndex;
        public DB2Flags Flags => parser.Flags;

        #endregion

        #region Constructors
        public Storage(string fileName) : this(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) { }

        public Storage(Stream stream) : this(new DBParser(stream)) => parser.ClearCache();

        public Storage(DBParser dbParser)
        {
            parser = dbParser;
            parser.PopulateRecords(this);
        }
        #endregion

        #region Methods

        public void Save(string fileName) => parser.WriteRecords(this, fileName);

        public void Save(Stream stream) => parser.WriteRecords(this, stream);

        #endregion
    }
}