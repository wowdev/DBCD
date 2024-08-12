using BenchmarkDotNet.Attributes;
using DBCD.Providers;

namespace DBCD.Benchmark.Benchmarks
{
    [MemoryDiagnoser]
    public class ReadingBenchmark
    {
        private static readonly FilesystemDBDProvider localDBDProvider = new FilesystemDBDProvider("D:\\Projects\\WoWDBDefs\\definitions");
        private static readonly FilesystemDBCProvider localDBCProvider = new FilesystemDBCProvider("D:\\Projects\\DBCDStaging\\DBCD.Tests\\bin\\Debug\\net8.0\\DBCCache\\11.0.2.56044");
        private readonly string[] allDB2s = Directory.GetFiles("D:\\Projects\\DBCDStaging\\DBCD.Tests\\bin\\Debug\\net8.0\\DBCCache\\11.0.2.56044", "*.db2", SearchOption.AllDirectories).Select(x => Path.GetFileNameWithoutExtension(x)).ToArray();
        private readonly string build = "11.0.2.56044";

        [Benchmark]
        public int TestReadingAllDB2s()
        {
            var inputDBCD = new DBCD(localDBCProvider, localDBDProvider);

            //var build = "3.3.5.12340"; // WDBC
            //var build = "6.0.1.18179"; // WDB2
            //var build = "7.0.1.20740"; // WDB3, only 1 DBD sadly
            //var build = "7.0.1.20810"; // WDB4, only 2 DBDs sadly
            //var build = "7.2.0.23436"; // WDB5, only Map.db2
            //var build = "7.3.5.25928"; // WDB6
            //var build = "7.3.5.25928"; // WDC1
            //var build = "8.0.1.26231"; // WDC2
            //var build = "9.1.0.39653"; // WDC3
            //var build = "10.1.0.48480"; // WDC4
            var build = "11.0.2.56044"; // WDC5
        
            foreach (var tableName in allDB2s)
            {
                if (tableName == "UnitTestSparse")
                    continue;

                if (!localDBDProvider.ContainsBuild(tableName, build))
                    continue;

                var storage = inputDBCD.Load(tableName, build);
            }

            return allDB2s.Count();
        }
    }
}
