using System.IO;
using DBCD.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DBCD.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var githubDBDProvider = new GithubDBDProvider();
            var dbcProvider = new TestDBCProvider(@"E:\");

            DBCD dbcd = new DBCD(dbcProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("Achievement");

            //IDBCDStorage storage = dbcd.Load("LockType", "1.12.1.5875", Locale.EnUS);
        }
    }
}
