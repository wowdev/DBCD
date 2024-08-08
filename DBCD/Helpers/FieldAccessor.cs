using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;

namespace DBCD.Helpers
{
    internal class FieldAccessor
    {
        public IEnumerable<string> FieldNames => _accessors.Keys;

        private readonly Dictionary<string, Func<object, dynamic>> _accessors;
        private readonly CultureInfo _convertCulture;

        public FieldAccessor(Type type, string[] fields)
        {
            _accessors = new Dictionary<string, Func<object, dynamic>>();
            _convertCulture = CultureInfo.InvariantCulture;

            var ownerParameter = Expression.Parameter(typeof(object));

            foreach (var field in fields)
            {
                var fieldExpression = Expression.Field(Expression.Convert(ownerParameter, type), field);
                var conversionExpression = Expression.Convert(fieldExpression, typeof(object));
                var accessorExpression = Expression.Lambda<Func<object, dynamic>>(conversionExpression, ownerParameter);

                _accessors.Add(field, accessorExpression.Compile());
            }
        }


        public object this[object obj, string key]
        {
            get => _accessors[key](obj);
        }

        public bool TryGetMember(object obj, string field, out object value)
        {
            if (_accessors.TryGetValue(field, out var accessor))
            {
                value = accessor(obj);
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public T GetMemberAs<T>(object obj, string field)
        {
            var value = _accessors[field](obj);

            if (value is T direct)
                return direct;

            if (value is Array array)
            {
                return ConvertArray<T>(array);
            }
            else
            {
                return (T)Convert.ChangeType(value, typeof(T), _convertCulture);
            }
        }


        private T ConvertArray<T>(Array array)
        {
            var type = typeof(T);
            if (!type.IsArray)
                throw new InvalidCastException($"Cannot convert type '{array.GetType().Name}' to '{type.Name}'");

            var elementType = type.GetElementType();
            var result = Array.CreateInstance(elementType, array.Length);

            for (int i = 0; i < result.Length; i++)
            {
                object value = Convert.ChangeType(array.GetValue(i), elementType, _convertCulture);
                result.SetValue(value, i);
            }

            return (T)(object)result;
        }
    }
}
