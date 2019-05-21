using System.Collections.Generic;
using System.IO;

namespace DBFileReaderLib
{
    public class Storage<T> : SortedDictionary<int, T> where T : class, new()
    {
        public Storage(string fileName) : this(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) { }

        public Storage(Stream stream) : this(new DBReader(stream)) { }

        public Storage(DBReader dbReader) => dbReader.PopulateRecords(this);
    }
}
