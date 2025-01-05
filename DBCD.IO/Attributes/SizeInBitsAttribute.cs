using System;
using System.Collections.Generic;
using System.Text;

namespace DBCD.IO.Attributes
{
    public class SizeInBitsAttribute: Attribute
    {
        public readonly ushort Size;

        public SizeInBitsAttribute(ushort size) => Size = size;
    }
}
