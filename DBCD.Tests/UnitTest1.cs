using DBCD.Providers;
using DBCD.IO.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using DBCD.IO;
using System.Linq;

namespace DBCD.Tests
{
    [TestClass]
    public class UnitTest1
    {
        static GithubDBDProvider githubDBDProvider = new(true);
        static readonly WagoDBCProvider wagoDBCProvider = new();

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
