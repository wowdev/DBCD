using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;

namespace DBCD.Helpers
{
    internal class FieldAccessor
    {
        public IEnumerable<string> FieldNames => _getters.Keys;

        private readonly Dictionary<string, Func<object, dynamic>> _getters;
        private readonly Dictionary<string, Action<object, object>> _setters;

        private readonly CultureInfo _convertCulture;

        public FieldAccessor(Type type, string[] fields)
        {
            _getters = new Dictionary<string, Func<object, dynamic>>();
            _setters = new Dictionary<string, Action<object, object>>();
            _convertCulture = CultureInfo.InvariantCulture;

            var ownerParameter = Expression.Parameter(typeof(object));
            var valueParameter = Expression.Parameter(typeof(object));

            foreach (var field in fields)
            {
                var fieldExpression = Expression.Field(Expression.Convert(ownerParameter, type), field);

                var conversionExpression = Expression.Convert(fieldExpression, typeof(object));
                var getterExpression = Expression.Lambda<Func<object, dynamic>>(conversionExpression, ownerParameter);
                _getters.Add(field, getterExpression.Compile());


                var assignExpression = Expression.Assign(fieldExpression, Expression.Convert(valueParameter, fieldExpression.Type));
                var setterExpression = Expression.Lambda<Action<object, object>>(assignExpression, ownerParameter, valueParameter);
                _setters.Add(field, setterExpression.Compile());
            }
        }

        public object this[object obj, string key]
        {
            get => _getters[key](obj);
            set => _setters[key](obj, value);
        }

        public bool TryGetMember(object obj, string field, out object value)
        {
            if (_getters.TryGetValue(field, out var getter))
            {
                value = getter(obj);
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public bool TrySetMember(object obj, string field, object value)
        {
            if (_setters.TryGetValue(field, out var setter))
            {
                setter(obj, value);
                return true;
            }

            return false;
        }

        public T GetMemberAs<T>(object obj, string field)
        {
            var value = _getters[field](obj);

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