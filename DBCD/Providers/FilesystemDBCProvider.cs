using System.IO;

namespace DBCD.Providers
{
    /// <summary>
    /// Loads DB2 files from a local directory.
    /// </summary>
    public class FilesystemDBCProvider : IDBCProvider
    {
        private readonly string directory;

        public FilesystemDBCProvider(string directory) => this.directory = directory;

        public Stream StreamForTableName(string tableName, string build) => File.OpenRead(Path.Combine(directory, $"{tableName}.db2"));
    }
}
