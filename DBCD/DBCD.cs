using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DBCD.Providers;

namespace DBCD
{

    public class DBCD
    {
        private IDBCProvider dbcProvider;
        private IDBDProvider dbdProvider;
        public DBCD(IDBCProvider dbcProvider, IDBDProvider dbdProvider)
        {
            this.dbcProvider = dbcProvider;
            this.dbdProvider = dbdProvider;
        }

        public IDBCDStorage Load(string tableName)
        {
            var dbcStream = this.dbcProvider.StreamForTableName(tableName);
            var dbdStream = this.dbdProvider.StreamForTableName(tableName);

            var builder = new DBCDBuilder();

            // Since passing the stream to DB2Files.Net will close it after read, we need to make a copy here.
            var dbcStreamCopy = new MemoryStream();
            dbcStream.CopyTo(dbcStreamCopy);

            // Reset stream position to zero.
            dbcStream.Position = 0;
            dbcStreamCopy.Position = 0;

            var definition = builder.Build(dbcStream, dbdStream, tableName);

            var type = typeof(DBCDStorage<>).MakeGenericType(definition.Item1);

            return (IDBCDStorage)Activator.CreateInstance(type, new object[2] {
                dbcStreamCopy, // TODO: Fix the stream being unreadable at this point.
                definition.Item2
            });
        }
    }
}