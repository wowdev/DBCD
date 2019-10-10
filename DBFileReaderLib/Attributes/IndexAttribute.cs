using System;

namespace DBFileReaderLib.Attributes
{
    public class IndexAttribute : Attribute
    {
        public readonly bool Inline;

        public IndexAttribute(bool inline) => Inline = inline;
    }
}
