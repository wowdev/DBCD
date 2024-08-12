using BenchmarkDotNet.Attributes;
using DBCD.Benchmark.Utilities;
using DBCD.Providers;

namespace DBCD.Benchmark.Benchmarks
{
    [MemoryDiagnoser]
    public class WritingBenchmark
    {
        public static GithubDBDProvider DBDProvider { get; } = new GithubDBDProvider(true);
        public static string InputPath { get; } = $"{Directory.GetCurrentDirectory()}\\dbc";
        public static DBCD InputDBCD { get; } = new DBCD(new FilesystemDBCProvider(InputPath), DBDProvider);
        public static DBCD SavedDBCD { get; } = new DBCD(new FilesystemDBCProvider("tmp"), DBDProvider);

        public static string Build { get; } = "9.1.0.39653";

        [Benchmark]
        public void TestWritingAllDB2s()
        {
            string[] allDB2s = Directory.GetFiles(InputPath, "*.db2", SearchOption.TopDirectoryOnly);

            if (Directory.Exists("tmp"))
                Directory.Delete("tmp", true);
            Directory.CreateDirectory("tmp");

            foreach (var db2File in allDB2s)
            {
                if (Utilities.IO.TryGetExactPath(db2File, out string exactPath))
                {
                    var tableName = Path.GetFileNameWithoutExtension(exactPath);

                    var originalStorage = InputDBCD.Load(tableName, Build);
                    originalStorage.Save($"tmp/{tableName}.db2");
                }
            }

            Directory.Delete("tmp", true);
        }
    }
}
