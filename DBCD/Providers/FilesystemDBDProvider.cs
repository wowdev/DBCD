using System.IO;

namespace DBCD.Providers
{
    /// <summary>
    /// Loads DBD files from a local directory, such as a checked out copy of WoWDBDefs.
    /// </summary>
    public class FilesystemDBDProvider : IDBDProvider
    {
        private readonly string directory;

        public FilesystemDBDProvider(string directory) => this.directory = directory;

        public Stream StreamForTableName(string tableName, string build = null) => File.OpenRead(Path.Combine(directory, $"{tableName}.dbd"));
    }
}
