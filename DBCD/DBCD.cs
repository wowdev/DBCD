using DBCD.Providers;
using DBFileReaderLib;
using System;

namespace DBCD
{

    public class DBCD
    {
        private readonly IDBCProvider dbcProvider;
        private readonly IDBDProvider dbdProvider;
        public DBCD(IDBCProvider dbcProvider, IDBDProvider dbdProvider)
        {
            this.dbcProvider = dbcProvider;
            this.dbdProvider = dbdProvider;
        }

        public IDBCDStorage Load(string tableName, string build = null)
        {
            var dbcStream = this.dbcProvider.StreamForTableName(tableName, build);
            var dbdStream = this.dbdProvider.StreamForTableName(tableName, build);

            var builder = new DBCDBuilder();

            var dbReader = new DBReader(dbcStream);
            var definition = builder.Build(dbReader, dbdStream, tableName, build);

            var type = typeof(DBCDStorage<>).MakeGenericType(definition.Item1);

            return (IDBCDStorage)Activator.CreateInstance(type, new object[2] {
                dbReader,
                definition.Item2
            });
        }
    }
}