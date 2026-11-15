using System;
using System.Runtime.CompilerServices;

namespace DBCD.IO.Common;

public interface IPrimitiveSetter<T>
{
    // Value type setter for value type objects
    void SetValue<TValue>(T refObject, TValue value) where TValue : struct;
    
    // Fallback setter for value type assignments
    void SetObject(T target, object value);
}

public class PrimitiveSetter<T, TField> : IPrimitiveSetter<T>
{
    private readonly Action<T, TField> _setter;

    public PrimitiveSetter(Action<T, TField> setter)
    {
        _setter = setter;
    }
    
    public void SetValue<TValue>(T target, TValue value) where TValue : struct
    {
        if (typeof(TValue) == typeof(TField))
            _setter(target, Unsafe.As<TValue, TField>(ref value));
        else
            _setter(target, (TField)Convert.ChangeType(value, typeof(TField)));
    }

    public void SetObject(T target, object value)
    {
        _setter(target, (TField)value);
    }
}