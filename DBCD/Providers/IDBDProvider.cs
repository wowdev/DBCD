using System.IO;

namespace DBCD.Providers
{
    public interface IDBDProvider
    {
        Stream StreamForTableName(string tableName, string build = null);
    }
}