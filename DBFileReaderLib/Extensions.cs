using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DBFileReaderLib
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

        public static T GetAttribute<T>(this FieldInfo fieldInfo) where T : Attribute
        {
            return Attribute.GetCustomAttribute(fieldInfo, typeof(T)) as T;
        }

        public static T Read<T>(this BinaryReader reader) where T : struct
        {
            byte[] result = reader.ReadBytes(Unsafe.SizeOf<T>());
            return Unsafe.ReadUnaligned<T>(ref result[0]);
        }

        public static T[] ReadArray<T>(this BinaryReader reader) where T : struct
        {
            int numBytes = (int)reader.ReadInt64();

            byte[] result = reader.ReadBytes(numBytes);

            reader.BaseStream.Position += (0 - numBytes) & 0x07;
            return result.CopyTo<T>();
        }

        public static T[] ReadArray<T>(this BinaryReader reader, int size) where T : struct
        {
            int numBytes = Marshal.SizeOf<T>() * size;

            byte[] result = reader.ReadBytes(numBytes);
            return result.CopyTo<T>();
        }

        public static unsafe T[] CopyTo<T>(this byte[] src) where T : struct
        {
            T[] result = new T[src.Length / Unsafe.SizeOf<T>()];

            if (src.Length > 0)
                Unsafe.CopyBlockUnaligned(Unsafe.AsPointer(ref result[0]), Unsafe.AsPointer(ref src[0]), (uint)src.Length);

            return result;
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

        public static byte[] ToByteArray(this string str)
        {
            str = str.Replace(" ", string.Empty);

            var res = new byte[str.Length / 2];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = Convert.ToByte(str.Substring(i * 2, 2), 16);
            }
            return res;
        }
    }
}
