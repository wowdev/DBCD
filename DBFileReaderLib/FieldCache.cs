using System;
using System.Reflection;
using DBFileReaderLib.Attributes;

namespace DBFileReaderLib
{
    class FieldCache<T>
    {
        public readonly FieldInfo Field;
        public readonly bool IsArray = false;
        public readonly bool IndexMapField = false;
        public readonly Action<T, object> Setter;

        public int Cardinality { get; set; } = 1;

        public FieldCache(FieldInfo field)
        {
            Field = field;
            IsArray = field.FieldType.IsArray;
            Setter = field.GetSetter<T>();
            IndexMapField = Attribute.IsDefined(field, typeof(IndexAttribute));
            Cardinality = GetCardinality(field);
        }

        private int GetCardinality(FieldInfo field)
        {
            var attr = Attribute.GetCustomAttribute(field, typeof(CardinalityAttribute)) as CardinalityAttribute;
            return Math.Max(attr?.Count ?? 1, 1);
        }
    }
}
