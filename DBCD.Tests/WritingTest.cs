using DBCD.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace DBCD.Tests
{
    [TestClass]
    public class WritingTest
    {
        public static string InputPath { get; } = $"{Directory.GetCurrentDirectory()}\\DBCCache";

        public static string OutputPath = $"{Directory.GetCurrentDirectory()}\\tmp";

        public static WagoDBCProvider wagoDBCProvider = new();
        static GithubDBDProvider githubDBDProvider = new(true);

        [ClassInitialize()]
        public static void ClassInit(TestContext context)
        {
            if (Directory.Exists(OutputPath))
            {
                Directory.Delete(OutputPath, true);
            }
            Directory.CreateDirectory(OutputPath);
        }

        [ClassCleanup()]
        public static void ClassCleanup()
        {
            Directory.Delete(OutputPath, true);
        }


        [TestMethod]
        public void TestWritingNewRowDb2()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("AlliedRace", "9.2.7.45745");

            storage.Add(700000, storage.ConstructRow(700000));
            storage.Save(Path.Join(OutputPath, "AlliedRace.db2"));

            DBCD localDbcd = new(new FilesystemDBCProvider(OutputPath), githubDBDProvider);
            IDBCDStorage outputStorage = localDbcd.Load("AlliedRace", "9.2.7.45745");

            Assert.AreEqual(11, outputStorage.Count);
        }

        [TestMethod]
        public void TestWritingNewRowDb2WithArrayField()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("ItemDisplayInfo", "9.2.7.45745");

            storage.Add(700000, storage.ConstructRow(700000));
            storage.Save(Path.Join(OutputPath, "ItemDisplayInfo.db2"));

            DBCD localDbcd = new(new FilesystemDBCProvider(OutputPath), githubDBDProvider);
            IDBCDStorage outputStorage = localDbcd.Load("ItemDisplayInfo", "9.2.7.45745");

            Assert.AreEqual(116146, outputStorage.Count);
        }

        [TestMethod]
        public void TestSavingSameStorageTwice()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("AlliedRace", "9.2.7.45745");
            storage.Save(Path.Join(OutputPath, "AlliedRace.db2"));
            storage.Save(Path.Join(OutputPath, "AlliedRace.db2"));
        }

        [TestMethod]
        public void TestWritingAllDB2s()
        {
            return; // Only run this test manually

            var localDBDProvider = new FilesystemDBDProvider("D:\\Projects\\WoWDBDefs\\definitions");

            //var build = "3.3.5.12340"; // WDBC
            //var build = "6.0.1.18179"; // WDB2
            //var build = "7.0.1.20740"; // WDB3, TODO: Find DBDs for a DB2
            //var build = "7.0.1.20810"; // WDB4, TODO: Find DBDs for a DB2
            //var build = "7.0.3.21479"; // WDB5, TODO: Find DBDs for a DB2
            //var build = "7.2.0.23436"; // WDB6
            //var build = "7.3.5.25928"; // WDC1
            //var build = "8.0.1.26231"; // WDC2
            var build = "9.2.7.45745"; // WDC3
            //var build = "10.1.0.48480"; // WDC4
            //var build = "11.0.2.56044"; // WDC5

            var allDB2s = wagoDBCProvider.GetAllTableNames();

            if (Directory.Exists("tmp"))
                Directory.Delete("tmp", true);

            Directory.CreateDirectory("tmp");

            var localDBCProvider = new FilesystemDBCProvider(Path.Combine("DBCCache", build));
            var tmpDBCProvider = new FilesystemDBCProvider("tmp");

            var InputDBCD = new DBCD(localDBCProvider, localDBDProvider);
            var SavedDBCD = new DBCD(tmpDBCProvider, localDBDProvider);

            var attemptedTables = 0;
            var successfulTables = 0;
            var identicalTables = 0;

            foreach (var tableName in allDB2s)
            {
                if (!localDBDProvider.ContainsBuild(tableName, build))
                    continue;

                if (tableName == "UnitTestSparse")
                    continue;

                var originalValues = new List<DBCDRow>();

                attemptedTables++;

                try
                {
                    var originalStorage = InputDBCD.Load(tableName, build);

                    //if(tableName == "ModelFileData")
                    //{
                    //    var row = originalStorage.ConstructRow(4252801);
                    //    row["FileDataID"] = 4252801;
                    //    row["Flags"] = (byte)0;
                    //    row["LodCount"] = (byte)3;
                    //    row["ModelResourcesID"] = (uint)62664;
                    //}

                    originalValues.AddRange(originalStorage.Values);
                    originalStorage.Save($"tmp/{tableName}.db2");
                }
                catch (FileNotFoundException e)
                {
                    // This is not a reading test, I could not care less
                    attemptedTables--;
                    continue;
                }
                catch (AggregateException e)
                {
                    if (e.InnerException is HttpRequestException)
                    {
                        // This is not a reading test, I could not care less
                        attemptedTables--;
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("Failed to write " + tableName + " for build " + build + ": " + e.Message + "\n" + e.StackTrace);
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to write " + tableName + " for build " + build + ": " + e.Message + "\n" + e.StackTrace);
                    continue;
                }

                //try
                //{
                var savedStorage = SavedDBCD.Load(tableName, build);
                successfulTables++;
                // Lazy compare
                var originalJson = JsonConvert.SerializeObject(originalValues, Formatting.Indented);
                var newJson = JsonConvert.SerializeObject(savedStorage.Values, Formatting.Indented);
                if (originalJson != newJson)
                {
                    File.WriteAllText("original.json", originalJson);
                    File.WriteAllText("new.json", newJson);

                    throw new InvalidDataException($"The saved storage {tableName} should not differ from the original one!");
                }

                using (var originalStream = localDBCProvider.StreamForTableName(tableName, build))
                using (var originalMS = new MemoryStream())
                using (var savedStream = tmpDBCProvider.StreamForTableName(tableName, build))
                using (var savedMS = new MemoryStream())
                {
                    if (originalStream.Length != savedStream.Length)
                    {
                        Console.WriteLine(originalStream.Length + " vs " + savedStream.Length + " for " + tableName + " " + build);
                        continue;
                    }

                    originalStream.CopyTo(originalMS);
                    originalStream.Position = 0;

                    savedStream.CopyTo(savedMS);
                    savedStream.Position = 0;

                    var originalBytes = originalMS.ToArray();
                    var savedBytes = savedMS.ToArray();

                    if (!originalBytes.SequenceEqual(savedBytes))
                        Console.WriteLine("Different bytes for " + tableName + " " + build);
                    else
                        identicalTables++;
                }
                //}
                //catch (Exception e)
                //{
                //    Console.WriteLine("Failed to load rewritten " + tableName + " for build " + build + ": " + e.Message + "\n" + e.StackTrace);
                //}
            }

            Console.WriteLine(successfulTables + "/" + attemptedTables + " written succesfully");
            Console.WriteLine(identicalTables + "/" + successfulTables + " identical (" + (successfulTables - identicalTables) + " different)");

            Assert.AreEqual(attemptedTables, successfulTables);

            //Directory.Delete("tmp", true);
        }
    }
}
