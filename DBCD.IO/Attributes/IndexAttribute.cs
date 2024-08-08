using System;

namespace DBCD.IO.Attributes
{
    public class IndexAttribute : Attribute
    {
        public readonly bool NonInline;

        public IndexAttribute(bool noninline) => NonInline = noninline;
    }
}
