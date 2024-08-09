using BenchmarkDotNet.Attributes;
using DBCD.Benchmark.Utilities;
using DBCD.Providers;

namespace DBCD.Benchmark.Benchmarks
{
    [MemoryDiagnoser]
    public class ReadingBenchmark
    {
        public static GithubDBDProvider DBDProvider { get; } = new GithubDBDProvider(true);
        public static string InputPath { get; } = $"{Directory.GetCurrentDirectory()}\\dbc";
        public static DBCD InputDBCD { get; } = new DBCD(new FilesystemDBCProvider(InputPath), DBDProvider);

        public static string Build { get; } = "11.0.2.55959";

        [Benchmark]
        public void TestReadingAllDB2s()
        {
            string[] allDB2s = Directory.GetFiles(InputPath, "*.db2", SearchOption.TopDirectoryOnly);

            foreach (var db2File in allDB2s)
            {
                if (Utilities.IO.TryGetExactPath(db2File, out string exactPath))
                {
                    var tableName = Path.GetFileNameWithoutExtension(exactPath);
                    var originalStorage = InputDBCD.Load(tableName, Build);
                }
            }
        }
    }
}
