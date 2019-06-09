using System;
using System.IO;
using System.Net.Http;

namespace DBCD.Providers
{
    public class GithubDBDProvider : IDBDProvider
    {
        private static Uri BaseURI = new Uri("https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/definitions/");
        private HttpClient client = new HttpClient();

        public GithubDBDProvider()
        {
            client.BaseAddress = BaseURI;
        }

        public Stream StreamForTableName(string tableName, string build = null)
        {
            var query = $"{tableName}.dbd";
            var bytes = client.GetByteArrayAsync(query).Result;

            return new MemoryStream(bytes);
        }
    }
}