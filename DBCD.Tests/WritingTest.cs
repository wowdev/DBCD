using DBCD.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace DBCD.Tests
{
    [TestClass]
    public class WritingTest
    {
        public static GithubDBDProvider DBDProvider { get; } = new GithubDBDProvider(true);
        public static string InputPath { get; } = $"{Directory.GetCurrentDirectory()}\\DBCCache";
        public static WagoDBCProvider wagoDBCProvider = new();
        public static DBCD InputDBCD { get; } = new DBCD(wagoDBCProvider, DBDProvider);
        public static DBCD SavedDBCD { get; } = new DBCD(new FilesystemDBCProvider("tmp"), DBDProvider);

        public static string Build { get; } = "9.1.0.39653";

        [TestMethod]
        public void TestWritingAllDB2s()
        {
            return; // Only run this test manually

            var allDB2s = wagoDBCProvider.GetAllTableNames();

            if (Directory.Exists("tmp"))
                Directory.Delete("tmp", true);

            Directory.CreateDirectory("tmp");

            foreach (var tableName in allDB2s)
            {
                if (tableName == "UnitTestSparse")
                    continue;

                // TODO: possible DBD being wrong
                if (tableName == "SummonProperties")
                    continue;

                var originalValues = new List<DBCDRow>();

                try
                {
                    var originalStorage = InputDBCD.Load(tableName, Build);
                    originalValues.AddRange(originalStorage.Values);
                    originalStorage.Save($"tmp/{tableName}.db2");
                }
                catch (FileNotFoundException e)
                {
                    // This is not a reading test, I could not care less
                    continue;
                }
                catch (AggregateException e)
                {
                    if (e.InnerException is HttpRequestException)
                    {
                        // This is not a reading test, I could not care less
                        continue;
                    }
                    else
                    {
                        throw e;
                    }
                }

                var savedStorage = SavedDBCD.Load(tableName, Build);

                // Lazy compare
                var originalJson = JsonConvert.SerializeObject(originalValues, Formatting.Indented);
                var newJson = JsonConvert.SerializeObject(savedStorage.Values, Formatting.Indented);
                if (originalJson != newJson)
                {
                    File.WriteAllText("original.json", originalJson);
                    File.WriteAllText("new.json", newJson);

                    throw new InvalidDataException($"The saved storage {tableName} should not differ from the original one!");
                }
            }


            Directory.Delete("tmp", true);
        }
    }
}
