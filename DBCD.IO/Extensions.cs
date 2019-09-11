using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace DBCD.IO
{
    static class Extensions
    {
        public static Action<T, object> GetSetter<T>(this FieldInfo fieldInfo)
        {
            var paramExpression = Expression.Parameter(typeof(T));
            var propertyExpression = Expression.Field(paramExpression, fieldInfo);
            var valueExpression = Expression.Parameter(typeof(object));
            var convertExpression = Expression.Convert(valueExpression, fieldInfo.FieldType);
            var assignExpression = Expression.Assign(propertyExpression, convertExpression);

            return Expression.Lambda<Action<T, object>>(assignExpression, paramExpression, valueExpression).Compile();
        }

        public static Func<T, object> GetGetter<T>(this FieldInfo fieldInfo)
        {
            var paramExpression = Expression.Parameter(typeof(T));
            var propertyExpression = Expression.Field(paramExpression, fieldInfo);
            var convertExpression = Expression.Convert(propertyExpression, typeof(object));

            return Expression.Lambda<Func<T, object>>(convertExpression, paramExpression).Compile();
        }

        public static FieldCache<T>[] ToFieldCache<T>(this Type type)
        {
            var fields = type.GetFields();

            var cache = new FieldCache<T>[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                cache[i] = new FieldCache<T>(fields[i]);

            return cache;
        }

        public static T GetAttribute<T>(this FieldInfo fieldInfo) where T : Attribute
        {
            return Attribute.GetCustomAttribute(fieldInfo, typeof(T)) as T;
        }

        public static T Read<T>(this BinaryReader reader) where T : struct
        {
            byte[] result = reader.ReadBytes(Unsafe.SizeOf<T>());
            return Unsafe.ReadUnaligned<T>(ref result[0]);
        }

        public static void Write<T>(this BinaryWriter writer, T value) where T : struct
        {
            byte[] buffer = new byte[Unsafe.SizeOf<T>()];
            Unsafe.WriteUnaligned(ref buffer[0], value);
            writer.Write(buffer);
        }

        public static unsafe T[] ReadArray<T>(this BinaryReader reader, int size) where T : struct
        {
            int sizeOf = Unsafe.SizeOf<T>();

            byte[] src = reader.ReadBytes(sizeOf * size);
            if (src.Length == 0)
                return new T[0];

            T[] result = new T[src.Length / sizeOf];
            Unsafe.CopyBlockUnaligned(Unsafe.AsPointer(ref result[0]), Unsafe.AsPointer(ref src[0]), (uint)src.Length);
            return result;
        }

        public static unsafe void WriteArray<T>(this BinaryWriter writer, T[] value) where T : struct
        {
            if (value.Length == 0)
                return;

            if (!(value is byte[] buffer))
            {
                buffer = new byte[value.Length * Unsafe.SizeOf<T>()];
                Unsafe.CopyBlockUnaligned(Unsafe.AsPointer(ref buffer[0]), Unsafe.AsPointer(ref value[0]), (uint)buffer.Length);
            }

            writer.Write(buffer);
        }

        public static bool HasFlagExt(this DB2Flags flag, DB2Flags valueToCheck)
        {
            return (flag & valueToCheck) == valueToCheck;
        }
    }

    static class CStringExtensions
    {
        /// <summary> Reads the NULL terminated string from
        /// the current stream and advances the current position of the stream by string length + 1.
        /// <seealso cref="BinaryReader.ReadString"/>
        /// </summary>
        public static string ReadCString(this BinaryReader reader)
        {
            return reader.ReadCString(Encoding.UTF8);
        }

        /// <summary> Reads the NULL terminated string from
        /// the current stream and advances the current position of the stream by string length + 1.
        /// <seealso cref="BinaryReader.ReadString"/>
        /// </summary>
        public static string ReadCString(this BinaryReader reader, Encoding encoding)
        {
            var bytes = new System.Collections.Generic.List<byte>(0x20);
            byte b;
            while ((b = reader.ReadByte()) != 0)
                bytes.Add(b);
            return encoding.GetString(bytes.ToArray());
        }

        public static void WriteCString(this BinaryWriter writer, string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            writer.Write(bytes);
            writer.Write((byte)0);
        }
    }
}
