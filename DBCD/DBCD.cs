using System;
using System.Collections;
using System.Collections.Generic;

namespace DBCD
{
    public class DBCD
    {
        private DBCProvider dbcProvider;
        private DBDProvider dbdProvider;
        public DBCD(DBCProvider dbcProvider, DBDProvider dbdProvider)
        {
            this.dbcProvider = dbcProvider;
            this.dbdProvider = dbdProvider;
        }

        public IDictionary Load(string filename)
        {
            var dbcStream = this.dbcProvider.StreamForFilename(filename);
            var dbdStream = this.dbdProvider.StreamForFilename(filename);

            var builder = new DBCDBuilder();
            var definition = builder.Build(dbcStream, dbdStream);

            var type = typeof(Storage<>).MakeGenericType(definition);

            return (IDictionary)Activator.CreateInstance(type, dbcStream);
        }
    }
}