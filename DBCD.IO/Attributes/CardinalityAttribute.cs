using System;

namespace DBCD.IO.Attributes
{
    public class CardinalityAttribute : Attribute
    {
        public readonly int Count;

        public CardinalityAttribute(int count) => Count = count;
    }
}
