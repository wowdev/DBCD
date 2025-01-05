
using System;
using System.Collections.Generic;
using System.Text;

namespace DBCD.IO.Attributes
{
    public class ForeignReferenceAttribute : Attribute
    {
        public readonly string ForeignTable;
        public readonly string ForeignColumn;

        public ForeignReferenceAttribute(string foreignTable, string foreignColumn) => (ForeignTable, ForeignColumn) = (foreignTable, foreignColumn);

    }
}
