using System.IO;
using DBCD.Providers;
using DBFileReaderLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DBCD.Tests
{
    [TestClass]
    public class UnitTest1
    {
        static GithubDBDProvider githubDBDProvider = new GithubDBDProvider();
        static TestDBCProvider dbcProvider = new TestDBCProvider(Directory.GetCurrentDirectory());

        // [TestMethod]
        // public void TestMethod1()
        // {
        //     DBCD dbcd = new DBCD(dbcProvider, githubDBDProvider);
        //     IDBCDStorage storage = dbcd.Load("ItemEffect");

        //     var i1 = storage[116161];
        //     var i2 = storage[116162];
        // }

        [TestMethod]
        public void TestHotfixApplying()
        {
            DBCD dbcd = new DBCD(dbcProvider, githubDBDProvider);

            var storage = dbcd.Load("ItemSparse");
            var hotfix = new HotfixReader("hotfix.bin");

            var countBefore = storage.Count;
            storage = storage.ApplyingHotfixes(hotfix);

            var countAfter = storage.Count;

            System.Console.WriteLine($"B: {countBefore} => A: {countAfter}");
        }

        [TestMethod]
        public void TestEncryptedInfo()
        {
            var githubDBDProvider = new GithubDBDProvider();
            var dbcProvider = new TestDBCProvider(Directory.GetCurrentDirectory());

            DBCD dbcd = new DBCD(dbcProvider, githubDBDProvider);

            var storage = dbcd.Load("SpellName");

            foreach (var section in storage.GetEncryptedSections())
            {
                System.Console.WriteLine($"Found encrypted secttion encrypted with key {section.Key} containing {section.Value} rows");
            }
        }
    }
}
