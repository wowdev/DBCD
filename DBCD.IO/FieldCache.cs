using DBCD.IO.Attributes;
using System;
using System.Reflection;

namespace DBCD.IO
{
    class FieldCache<T>
    {
        private readonly FieldInfo Field;
        public readonly bool IsArray = false;
        public readonly bool IsLocalisedString = false;
        public readonly Action<T, object> Setter;
        public readonly Func<T, object> Getter;
        public readonly LocaleAttribute LocaleInfo;

        public bool IsNonInlineRelation { get; set; } = false;
        public bool IsRelation { get; set; } = false;
        public bool IndexMapField { get; set; } = false;
        public int Cardinality { get; set; } = 1;

        // Type of the variable that is used to store the field
        // Might not match the information retrieved from client
        // metadata i.e. when field is a relation (as those are always uint32) 
        public readonly Type FieldType;
        // Type of the variable as defined in client metadata
        public readonly Type MetaDataFieldType;

        public FieldCache(FieldInfo field)
        {
            Field = field;
            IsArray = field.FieldType.IsArray;
            IsLocalisedString = GetStringInfo(field, out LocaleInfo);
            Setter = field.GetSetter<T>();
            Getter = field.GetGetter<T>();
            Cardinality = GetCardinality(field);

            IndexAttribute indexAttribute = (IndexAttribute)Attribute.GetCustomAttribute(field, typeof(IndexAttribute));
            IndexMapField = (indexAttribute != null) ? indexAttribute.NonInline : false;

            RelationAttribute relationAttribute = (RelationAttribute)Attribute.GetCustomAttribute(field, typeof(RelationAttribute));
            IsRelation = (relationAttribute != null);
            IsNonInlineRelation = IsRelation && relationAttribute.IsNonInline;
            FieldType = field.FieldType;
            MetaDataFieldType = IsNonInlineRelation ? relationAttribute.FieldType : FieldType;
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
