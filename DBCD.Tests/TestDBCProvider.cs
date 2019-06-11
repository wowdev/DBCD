using DBCD.Providers;
using System.IO;

namespace DBCD.Tests
{
    class TestDBCProvider : IDBCProvider
    {
        private readonly string Directory;

        public TestDBCProvider(string directory) => Directory = directory;

        public Stream StreamForTableName(string tableName, string build) => File.OpenRead(Path.Combine(Directory, tableName + ".db2"));
    }
}
