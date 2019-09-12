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
        [TestMethod]
        public void TestMethod1()
        {
            var githubDBDProvider = new GithubDBDProvider();
            var dbcProvider = new TestDBCProvider(@"E:\");

            DBCD dbcd = new DBCD(dbcProvider, githubDBDProvider);
            IDBCDStorage storage = dbcd.Load("Creature");

            //IDBCDStorage storage = dbcd.Load("LockType", "1.12.1.5875", Locale.EnUS);
        }
    }
}
