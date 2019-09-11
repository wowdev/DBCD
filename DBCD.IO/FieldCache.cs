using DBCD.IO.Attributes;
using System;
using System.Reflection;

namespace DBCD.IO
{
    class FieldCache<T>
    {
        public readonly FieldInfo Field;
        public readonly bool IsArray = false;
        public readonly bool IsLocalisedString = false;
        public readonly Action<T, object> Setter;
        public readonly Func<T, object> Getter;
        public readonly LocaleAttribute LocaleInfo;

        public bool IndexMapField { get; set; } = false;
        public int Cardinality { get; set; } = 1;

        public FieldCache(FieldInfo field)
        {
            Field = field;
            IsArray = field.FieldType.IsArray;
            IsLocalisedString = GetStringInfo(field, out LocaleInfo);
            Setter = field.GetSetter<T>();
            Getter = field.GetGetter<T>();
            IndexMapField = Attribute.IsDefined(field, typeof(IndexAttribute));
            Cardinality = GetCardinality(field);
        }

        private int GetCardinality(FieldInfo field)
        {
            var cardinality = field.GetAttribute<CardinalityAttribute>()?.Count;
            return cardinality > 0 ? cardinality.Value : 1;
        }

        private bool GetStringInfo(FieldInfo field, out LocaleAttribute attribute)
        {
            return (attribute = field.GetAttribute<LocaleAttribute>()) != null;
        }
    }
}
