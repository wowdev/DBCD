using System;

namespace DBFileReaderLib.Attributes
{
    public class RelationAttribute : Attribute
    {
        public readonly Type FieldType;

        public RelationAttribute(Type fieldType) => this.FieldType = fieldType;
    }
}
