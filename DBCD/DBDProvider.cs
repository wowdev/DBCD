using System.IO;

namespace DBCD
{
    public interface DBDProvider
    {
        Stream StreamForFilename(string filename);
    }
}