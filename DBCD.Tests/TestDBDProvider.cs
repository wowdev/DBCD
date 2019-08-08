using DBCD.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DBCD.Tests
{
    class TestDBDProvider : IDBDProvider
    {
        public Stream StreamForTableName(string tableName, string build = null)
        {
            return File.OpenRead(Path.Combine(@"C:\Users\TomSpearman\Downloads\WoWDBDefs\definitions", tableName + ".dbd"));
        }
    }
}
