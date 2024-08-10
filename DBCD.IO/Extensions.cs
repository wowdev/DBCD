using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
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

        public static T GetAttribute<T>(this FieldInfo fieldInfo) where T : Attribute
        {
            return Attribute.GetCustomAttribute(fieldInfo, typeof(T)) as T;
        }

        public static FieldCache<T>[] ToFieldCache<T>(this Type type)
        {
            var fields = type.GetFields();

            var cache = new FieldCache<T>[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                cache[i] = new FieldCache<T>(fields[i]);

            return cache;
        }

        public static T Read<T>(this BinaryReader reader) where T : struct
        {
            byte[] result = reader.ReadBytes(Unsafe.SizeOf<T>());
            return Unsafe.ReadUnaligned<T>(ref result[0]);
        }


        /// <summary>
        /// Reads a NUL-separated string table from the current stream
        /// </summary>
        /// <param name="StringTableSize">Size of the string table</param>
        /// <param name="usePos">Use WDC2-style position-base table key numbering</param>
        /// <param name="BaseOffset">Base offset to use for the string table keys</param>
        public static Dictionary<long, string> ReadStringTable(this BinaryReader reader, int stringTableSize, int baseOffset = 0, bool usePos = false)
        {
            var StringTable = new Dictionary<long, string>(stringTableSize / 0x20);

            if(stringTableSize == 0)
                return StringTable;

            var curOfs = 0;
            var decoded = Encoding.UTF8.GetString(reader.ReadBytes(stringTableSize));
            foreach (var str in decoded.Split('\0'))
            {
                if (curOfs == stringTableSize)
                    break;

                if(usePos)
                    StringTable[(reader.BaseStream.Position - stringTableSize) + curOfs] = str;
                else
                    StringTable[baseOffset + curOfs] = str;

                curOfs += Encoding.UTF8.GetByteCount(str) + 1;
            }

            return StringTable;
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
            int numBytes = Unsafe.SizeOf<T>() * size;

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

        public static void Write<T>(this BinaryWriter writer, T value) where T : struct
        {
            byte[] buffer = new byte[Unsafe.SizeOf<T>()];
            Unsafe.WriteUnaligned(ref buffer[0], value);
            writer.Write(buffer);
        }

        public static bool HasFlagExt(this DB2Flags flag, DB2Flags valueToCheck)
        {
            return (flag & valueToCheck) == valueToCheck;
        }

        public static T MaxOrDefault<T>(this ICollection<T> source)
        {
            return source.DefaultIfEmpty().Max();
        }

        public static T MinOrDefault<T>(this ICollection<T> source)
        {
            return source.DefaultIfEmpty().Min();
        }
    }

    static class CStringExtensions
    {
        /// <summary> Reads the NULL terminated string from
        /// the current stream and advances the current position of the stream by string length + 1.
        /// <seealso cref="BinaryReader.ReadString"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadCString(this BinaryReader reader)
        {
            return reader.ReadCString(Encoding.UTF8);
        }

        /// <summary> Reads the NULL terminated string from
        /// the current stream and advances the current position of the stream by string length + 1.
        /// <seealso cref="BinaryReader.ReadString"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    /// <summary>
    /// A <see langword="class"/> that provides extension methods for numeric types
    /// </summary>
    public static class NumericExtensions
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MostSignificantBit(this int n)
        {
            if (n == 0) return 1;
            else return ((int)(BitConverter.DoubleToInt64Bits(n) >> 52) & 0x7ff) - 1022;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MostSignificantBit(this uint n)
        {
            if (n == 0) return 1;
            else return ((int)(BitConverter.DoubleToInt64Bits(n) >> 52) & 0x7ff) - 1022;
        }

        /// <summary>
        /// Calculates the upper bound of the log base 2 of the input value
        /// </summary>
        /// <param name="n">The input value to compute the bound for (with n > 0)</param>
        public static int UpperBoundLog2(this int n) => 1 << MostSignificantBit(n);
    }
}
