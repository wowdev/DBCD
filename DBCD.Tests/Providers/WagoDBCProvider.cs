using System;
using System.IO;
using System.Net.Http;

namespace DBCD.Providers
{
    /// <summary>
    /// Retrieves and returns DB2 files from wago.tools with 1-day caching.
    /// </summary>
    public class WagoDBCProvider : IDBCProvider
    {
        private readonly HttpClient client = new();

        public Stream StreamForTableName(string tableName, string build)
        {
            uint fileDataID;

            // For tests, we only support a few tables. Instead of loading a listfile/manifest we just hardcode the IDs. Add more if needed.
            switch (tableName.ToLower())
            {
                case "itemsparse":
                    fileDataID = 1572924;
                    break;
                case "spellname":
                    fileDataID = 1990283;
                    break;
                case "map":
                    fileDataID = 1349477;
                    break;
                case "mapdifficulty":
                    fileDataID = 1367868;
                    break;
                default:
                    throw new Exception("FileDataID not known for table " + tableName);
            }

            if(!Directory.Exists("DBCCache"))
                Directory.CreateDirectory("DBCCache");

            var cacheFile = Path.Combine("DBCCache", tableName + "-" + build + ".db2");
            if (File.Exists(cacheFile))
            {
                var lastWrite = File.GetLastWriteTime(cacheFile);
                if (DateTime.Now - lastWrite < new TimeSpan(1, 0, 0, 0))
                    return new MemoryStream(File.ReadAllBytes(cacheFile));
            }

            var bytes = client.GetByteArrayAsync("https://wago.tools/api/casc/" + fileDataID + "?version=" + build).Result;
            File.WriteAllBytes(cacheFile, bytes);
            return new MemoryStream(bytes);
        }
    }
}
