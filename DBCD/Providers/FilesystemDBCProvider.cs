using System.IO;

namespace DBCD.Providers
{
    /// <summary>
    /// Loads DBC/DB2 files from a local directory.
    /// </summary>
    public class FilesystemDBCProvider : IDBCProvider
    {
        private readonly string directory;

        public FilesystemDBCProvider(string directory) => this.directory = directory;

        public Stream StreamForTableName(string tableName, string build)
        {
            if(File.Exists(Path.Combine(directory, $"{tableName}.db2")))
                return File.OpenRead(Path.Combine(directory, $"{tableName}.db2"));

            if(File.Exists(Path.Combine(directory, $"{tableName}.dbc")))
                return File.OpenRead(Path.Combine(directory, $"{tableName}.dbc"));

            throw new FileNotFoundException("Unable to find DBC/DB2 file on disk for table " + tableName);
        }
    }
}
