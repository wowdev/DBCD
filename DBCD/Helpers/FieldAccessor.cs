using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DBCD.Helpers
{
    internal class FieldAccessor
    {
        private readonly Dictionary<string, Func<object, dynamic>> _accessors;

        public FieldAccessor(Type type)
        {
            _accessors = new Dictionary<string, Func<object, dynamic>>();
            
            var fields = type.GetFields();
            var ownerParameter = Expression.Parameter(typeof(object));

            foreach (var field in fields)
            {
                var fieldExpression = Expression.Field(Expression.Convert(ownerParameter, type), field);
                var conversionExpression = Expression.Convert(fieldExpression, typeof(object));
                var accessorExpression = Expression.Lambda<Func<object, dynamic>>(conversionExpression, ownerParameter);

                _accessors.Add(field.Name, accessorExpression.Compile());
            }
        }


        public object this[object obj, string key]
        {
            get => _accessors[key].Invoke(obj);
        }

        public bool TryGetMember(object obj, string field, out object value)
        {
            if (_accessors.TryGetValue(field, out var accessor))
            {
                value = accessor.Invoke(obj);
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
    }
}
