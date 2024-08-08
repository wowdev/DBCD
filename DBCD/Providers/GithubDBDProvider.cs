using System;
using System.IO;
using System.Net.Http;

namespace DBCD.Providers
{
    public class GithubDBDProvider : IDBDProvider
    {
        private static Uri BaseURI = new Uri("https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/definitions/");
        private HttpClient client = new HttpClient();

        private static bool UseCache = false;
        private static string CachePath { get; } = "DBDCache/";
        private static readonly TimeSpan CacheExpiryTime = new TimeSpan(1, 0, 0, 0);

        public GithubDBDProvider(bool useCache = false)
        {
            UseCache = useCache;
            if(useCache && !Directory.Exists(CachePath))
                Directory.CreateDirectory(CachePath);

            client.BaseAddress = BaseURI;
        }

        public Stream StreamForTableName(string tableName, string build = null)
        {
            var query = $"{tableName}.dbd";

            if(UseCache)
            {
                var cacheFile = Path.Combine(CachePath, query);
                if(File.Exists(cacheFile))
                {
                    var lastWrite = File.GetLastWriteTime(cacheFile);
                    if(DateTime.Now - lastWrite < CacheExpiryTime)
                        return new MemoryStream(File.ReadAllBytes(cacheFile));
                }
            }

            var bytes = client.GetByteArrayAsync(query).Result;

            if(UseCache)
                File.WriteAllBytes(Path.Combine(CachePath, query), bytes);
            
            return new MemoryStream(bytes);
        }
    }
}