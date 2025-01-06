using DBCD.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace DBCD.Tests
{
    [TestClass]
    public class ReadingTest
    {
        static GithubDBDProvider githubDBDProvider = new(true);
        static readonly WagoDBCProvider wagoDBCProvider = new();

        // Disabled as 7.1.0 definitions are not yet generally available
        /*
        [TestMethod]
        public void TestWDB5ReadingNoIndexData()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("Achievement_Category", "7.1.0.23222");
            var row = storage[1];
            Assert.AreEqual("Statistics", row["Name_lang"]);
        }
        */
        [TestMethod]
        public void TestWDB5Reading()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("Map", "7.1.0.23222");
            var row = storage[451];
            Assert.AreEqual("development", row["Directory"]);
        }

        [TestMethod]
        public void TestWDC1Reading()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("Map", "7.3.5.25600");

            var row = storage[451];
            Assert.AreEqual("development", row["Directory"]);
        }

        [TestMethod]
        public void TestWDC2Reading()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("Map", "8.0.1.26231");

            var row = storage[451];
            Assert.AreEqual("development", row["Directory"]);
        }

        [TestMethod]
        public void TestWDC3Reading()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("Map", "9.2.7.45745");

            var row = storage[451];
            Assert.AreEqual("development", row["Directory"]);
        }

        [TestMethod]
        public void TestWDC4Reading()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("Map", "10.1.0.48480");

            var row = storage[2574];
            Assert.AreEqual("Dragon Isles", row["MapName_lang"]);
        }

        [TestMethod]
        public void TestWDC5Reading()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("Map", "10.2.5.52432");

            var row = storage[2574];
            Assert.AreEqual("Dragon Isles", row["MapName_lang"]);
        }

        [TestMethod]
        public void TestSparseReading()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("ItemSparse", "9.2.7.45745");

            var row = storage[132172];
            Assert.AreEqual("Crowbar", row["Display_lang"]);
        }

        [TestMethod]
        public void TestNonInlineRelation()
        {
            DBCD dbcd = new(wagoDBCProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("MapDifficulty", "9.2.7.45745");

            var row = storage[38];
            Assert.AreEqual(451, row["MapID"]);
        }

        [TestMethod]
        public void TestEncryptedInfo()
        {
            DBCD dbcd = new DBCD(wagoDBCProvider, githubDBDProvider);

            var storage = dbcd.Load("SpellName", "11.0.2.55959");

            foreach (var section in storage.GetEncryptedSections())
            {
                System.Console.WriteLine($"Found encrypted section encrypted with key {section.Key} containing {section.Value} rows");
            }
        }

        [TestMethod]
        public void TestGithubDBDProviderNoCache()
        {
            var noCacheProvider = new GithubDBDProvider(false);
            noCacheProvider.StreamForTableName("ItemSparse");
        }

        [TestMethod]
        public void TestGithubDBDProviderWithCache()
        {
            githubDBDProvider.StreamForTableName("ItemSparse");
        }

        [TestMethod]
        public void TestReadingAllDB2s()
        {
            return; // Only run this test manually
            var localDBDProvider = new FilesystemDBDProvider("D:\\Projects\\WoWDBDefs\\definitions");

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

            var localDBCProvider = new FilesystemDBCProvider(Path.Combine("DBCCache", build));
            var dbcd = new DBCD(localDBCProvider, localDBDProvider);
            var allDB2s = wagoDBCProvider.GetAllTableNames();

            var attemptedTables = 0;
            var successfulTables = 0;

            foreach (var tableName in allDB2s)
            {
                // I think this table is meant to crash the test, so we skip it
                if (tableName == "UnitTestSparse")
                    continue;

                if (!localDBDProvider.ContainsBuild(tableName, build))
                    continue;

                attemptedTables++;

                try
                {
                    var storage = dbcd.Load(tableName, build);
                    successfulTables++;
                }
                catch (FileNotFoundException e)
                {
                    Console.WriteLine($"Failed to load {tableName} for build {build}, does not exist in build.");
                    successfulTables++; // this counts
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to load " + tableName + " for build " + build + ": " + e.Message + "\n" + e.StackTrace);
                }
            }

            Assert.AreEqual(attemptedTables, successfulTables);
        }

        //[TestMethod]
        //public void TestHotfixApplying()
        //{
        //    DBCD dbcd = new DBCD(dbcProvider, githubDBDProvider);

        //    var storage = dbcd.Load("ItemSparse");
        //    var hotfix = new HotfixReader("hotfix.bin");

        //    var countBefore = storage.Count;
        //    storage = storage.ApplyingHotfixes(hotfix);

        //    var countAfter = storage.Count;

        //    System.Console.WriteLine($"B: {countBefore} => A: {countAfter}");
        //}


        //[TestMethod]
        //public void TestFilesystemDBDProvider()
        //{
        //    DBCD dbcd = new DBCD(dbcProvider, dbdProvider);
        //    var storage = dbcd.Load("SpellName", locale: Locale.EnUS);
        //    // Spell is present in Classic Era -> Retail: https://www.wowhead.com/spell=17/
        //    Assert.AreEqual("Power Word: Shield", storage[17]["Name_lang"]);
        //}
    }
}
