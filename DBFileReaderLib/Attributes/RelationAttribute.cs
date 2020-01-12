using System;

namespace DBFileReaderLib.Attributes
{
    public class NonInlineRelationAttribute : Attribute
    {
        public readonly Type FieldType;

        public NonInlineRelationAttribute(Type fieldType) => this.FieldType = fieldType;
    }
}
