using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace DBCD.Providers
{
    /// <summary>
    /// Retrieves and returns DB2 files from wago.tools with 1-day caching.
    /// </summary>
    public class WagoDBCProvider : IDBCProvider
    {
        private readonly HttpClient client = new();
        private readonly Dictionary<string, uint> DB2FileDataIDs = new();

        public WagoDBCProvider()
        {
            if (DB2FileDataIDs.Count == 0)
                LoadDBDManifest();
        }

        private struct DBDManifestEntry {
            public string tableName;
            public string tableHash;
            public uint dbcFileDataID;
            public uint db2FileDataID;
        }

        private void LoadDBDManifest()
        {
            var manifest = client.GetStringAsync("https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/manifest.json").Result;
            var dbdManifest = Newtonsoft.Json.JsonConvert.DeserializeObject<List<DBDManifestEntry>>(manifest);

            foreach(var entry in dbdManifest)
                DB2FileDataIDs[entry.tableName] = entry.db2FileDataID;
        }

        public string[] GetAllTableNames()
        {
            return DB2FileDataIDs.Keys.ToArray();
        }

        public Stream StreamForTableName(string tableName, string build)
        {
            if (!DB2FileDataIDs.TryGetValue(tableName, out uint fileDataID))
                throw new Exception("Unable to find table " + tableName + " in FDID lookup!");

            if(!Directory.Exists("DBCCache"))
                Directory.CreateDirectory("DBCCache");

            if (!Directory.Exists(Path.Combine("DBCCache", build)))
                Directory.CreateDirectory(Path.Combine("DBCCache", build));

            var cacheFile = Path.Combine("DBCCache", build, tableName + ".db2");
            if (File.Exists(cacheFile))
            {
                var lastWrite = File.GetLastWriteTime(cacheFile);
                if (DateTime.Now - lastWrite < new TimeSpan(1, 0, 0, 0))
                    return new MemoryStream(File.ReadAllBytes(cacheFile));
            }

            var bytes = client.GetByteArrayAsync("https://wago.tools/api/casc/" + fileDataID + "?version=" + build).Result;
            if (bytes.Length == 0 || (bytes.Length < 40 && Encoding.ASCII.GetString(bytes).Contains("error")))
                throw new FileNotFoundException();

            File.WriteAllBytes(cacheFile, bytes);
            return new MemoryStream(bytes);
        }
    }
}
