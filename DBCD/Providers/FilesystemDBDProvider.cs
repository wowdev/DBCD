using DBDefsLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DBCD.Providers
{
    /// <summary>
    /// Loads DBD files from a local directory, such as a checked out copy of WoWDBDefs.
    /// </summary>
    public class FilesystemDBDProvider : IDBDProvider
    {
        private readonly string directory;

        public FilesystemDBDProvider(string directory) => this.directory = directory;

        public Dictionary<(string, string), byte[]> Cache = new Dictionary<(string, string), byte[]>();

        /// <summary>
        /// Function that checks if a certain build exists in a DBD file. Note that this causes a full read/parse of the file.
        /// </summary>
        public bool ContainsBuild(string tableName, string build)
        {
            if (!File.Exists(Path.Combine(directory, $"{tableName}.dbd")))
                return false;

            var reader = new DBDReader();
            var definition = reader.Read(StreamForTableName(tableName));
            var targetBuild = new Build(build);

            foreach (var versionDefinition in definition.versionDefinitions)
            {
                if (versionDefinition.builds.Contains(targetBuild))
                    return true;

                if (versionDefinition.buildRanges.Any(range => range.Contains(targetBuild)))
                    return true;
            }

            return false;
        }

        public Stream StreamForTableName(string tableName, string build = null)
        {
            if (Cache.TryGetValue((tableName, build), out var cachedData))
                return new MemoryStream(cachedData);
            else
            {
                var data = File.ReadAllBytes(Path.Combine(directory, $"{tableName}.dbd"));
                Cache[(tableName, build)] = data;
                return new MemoryStream(data);
            }
        }
    }
}
