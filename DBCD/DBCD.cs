using DBCD.Providers;
using DBCD.IO;
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

        public IDBCDStorage Load(string tableName, string build = null, Locale locale = Locale.None)
        {
            var dbcStream = this.dbcProvider.StreamForTableName(tableName, build);
            var dbdStream = this.dbdProvider.StreamForTableName(tableName, build);

            var builder = new DBCDBuilder(locale);

            var dbReader = new DBParser(dbcStream);
            var definition = builder.Build(dbReader, dbdStream, tableName, build);

            var type = typeof(DBCDStorage<>).MakeGenericType(definition.Item1);

            return (IDBCDStorage)Activator.CreateInstance(type, new object[2] {
                dbReader,
                definition.Item2
            });
        }
    }

    public enum Locale
    {
        None = -1,
        EnUS = 0,
        EnGB = EnUS,
        KoKR = 1,
        FrFR = 2,
        DeDE = 3,
        EnCN = 4,
        ZhCN = EnCN,
        EnTW = 5,
        ZhTW = EnTW,
        EsES = 6,
        EsMX = 7,
        /* Available from TBC 2.1.0.6692 */
        RuRU = 8,
        PtPT = 10,
        PtBR = PtPT,
        ItIT = 11,
    }
}