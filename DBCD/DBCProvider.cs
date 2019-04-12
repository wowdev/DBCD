using System.IO;

namespace DBCD
{
    public interface DBCProvider
    {
        Stream StreamForFilename(string filename);
    }
}