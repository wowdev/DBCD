using System;
using System.IO;
using System.Net.Http;

namespace DBCD.Providers
{
    public static class GithubBDBDProvider
    {
        public static string BDBDUrl = "https://github.com/wowdev/WoWDBDefs/releases/latest/download/all.bdbd";

        private static string CachePath { get; } = "BDBDCache/";
        private static readonly TimeSpan CacheExpiryTime = new TimeSpan(1, 0, 0, 0);

        public static Stream GetStream(bool forceNew = false)
        {
            var currentFile = Path.Combine(CachePath, "all.bdbd");
            if (File.Exists(currentFile))
            {
                var fileInfo = new FileInfo(currentFile);
                if (fileInfo.Length == 0)
                {
                    File.Delete(currentFile);
                }
                else
                {
                    if (!forceNew && DateTime.Now - fileInfo.LastWriteTime < CacheExpiryTime)
                        return new MemoryStream(File.ReadAllBytes(currentFile));
                }
            }

            if (!Directory.Exists(CachePath))
                Directory.CreateDirectory(CachePath);

            var bdbdStream = new MemoryStream();
            using (var fileStream = new FileStream(currentFile, FileMode.Create, FileAccess.Write))
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(BDBDUrl).Result;
                response.EnsureSuccessStatusCode();
                response.Content.CopyToAsync(bdbdStream).Wait();
                bdbdStream.CopyTo(fileStream);
                bdbdStream.Position = 0;
            }
            return bdbdStream;
        }
    }
}
