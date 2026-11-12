using System.Collections.Generic;
using System.IO;

namespace DBCD.Providers
{
    /// <summary>
    /// Loads DBC/DB2 files from a local directory.
    /// </summary>
    public class FilesystemDBCProvider : IDBCProvider
    {
        private readonly string Directory;
        private readonly bool UseCache;
        public Dictionary<(string, string), byte[]> Cache = new Dictionary<(string, string), byte[]>();

        public FilesystemDBCProvider(string directory, bool useCache = false) => (this.Directory, this.UseCache) = (directory, useCache);

        public Stream StreamForTableName(string tableName, string build)
        {
            if (UseCache && Cache.TryGetValue((tableName, build), out var cachedData))
            {
#if NET11_0_OR_GREATER
                return new ReadOnlyMemoryStream(cachedData);
#else
                return new MemoryStream(cachedData);
#endif
            }
            else
            {
                if (File.Exists(Path.Combine(Directory, $"{tableName}.db2")))
                {
                    var bytes = File.ReadAllBytes(Path.Combine(Directory, $"{tableName}.db2"));
                    if (UseCache)
                        Cache[(tableName, build)] = bytes;

#if NET11_0_OR_GREATER
                    return new ReadOnlyMemoryStream(bytes);
#else
                    return new MemoryStream(bytes);
#endif
                }

                if (File.Exists(Path.Combine(Directory, $"{tableName}.dbc")))
                {
                    var bytes = File.ReadAllBytes(Path.Combine(Directory, $"{tableName}.dbc"));
                    if(UseCache)
                        Cache[(tableName, build)] = File.ReadAllBytes(Path.Combine(Directory, $"{tableName}.dbc"));
#if NET11_0_OR_GREATER
                    return new ReadOnlyMemoryStream(bytes);
#else
                    return new MemoryStream(bytes);
#endif
                }

                throw new FileNotFoundException("Unable to find DBC/DB2 file on disk for table " + tableName);
            }
        }
    }
}
