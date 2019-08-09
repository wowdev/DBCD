using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS0649
#pragma warning disable IDE0044

namespace DBCD.IO.Common
{
    struct FieldMetaData
    {
        public short Bits;
        public short Offset;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct ColumnMetaData
    {
        [FieldOffset(0)]
        public ushort RecordOffset;
        [FieldOffset(2)]
        public ushort Size;
        [FieldOffset(4)]
        public uint AdditionalDataSize;
        [FieldOffset(8)]
        public CompressionType CompressionType;
        [FieldOffset(12)]
        public ColumnCompressionData_Immediate Immediate;
        [FieldOffset(12)]
        public ColumnCompressionData_Pallet Pallet;
        [FieldOffset(12)]
        public ColumnCompressionData_Common Common;
    }

    struct ColumnCompressionData_Immediate
    {
        public int BitOffset;
        public int BitWidth;
        public int Flags; // 0x1 signed
    }

    struct ColumnCompressionData_Pallet
    {
        public int BitOffset;
        public int BitWidth;
        public int Cardinality;
    }

    struct ColumnCompressionData_Common
    {
        public Value32 DefaultValue;
        public int B;
        public int C;
    }

    struct Value32
    {
        unsafe fixed byte Value[4];

        public T GetValue<T>() where T : struct
        {
            unsafe
            {
                fixed (byte* ptr = Value)
                    return Unsafe.ReadUnaligned<T>(ptr);
            }
        }

        public static unsafe Value32 Create<T>(T obj) where T : unmanaged
        {
            return *(Value32*)&obj;
        }

        public static unsafe Value32 Create(object obj)
        {
            if (obj is byte b)
                return *(Value32*)&b;
            else if (obj is sbyte sb)
                return *(Value32*)&sb;
            else if (obj is short s)
                return *(Value32*)&s;
            else if (obj is ushort us)
                return *(Value32*)&us;
            else if (obj is int i)
                return *(Value32*)&i;
            else if (obj is uint ui)
                return *(Value32*)&ui;
            else if (obj is long l)
                return *(Value32*)&l;
            else if (obj is ulong ul)
                return *(Value32*)&ul;
            else if (obj is float f)
                return *(Value32*)&f;
            else
                throw new System.Exception("Invalid type");
        }
    }

    struct Value64
    {
        unsafe fixed byte Value[8];

        public T GetValue<T>() where T : struct
        {
            unsafe
            {
                fixed (byte* ptr = Value)
                    return Unsafe.ReadUnaligned<T>(ptr);
            }
        }
    }

    enum CompressionType
    {
        None = 0,
        Immediate = 1,
        Common = 2,
        Pallet = 3,
        PalletArray = 4,
        SignedImmediate = 5
    }

    struct ReferenceEntry
    {
        public int Id;
        public int Index;
    }

    class ReferenceData
    {
        public int NumRecords { get; set; }
        public int MinId { get; set; }
        public int MaxId { get; set; }
        public ReferenceEntry[] Entries { get; set; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct SparseEntry
    {
        public uint Offset;
        public ushort Size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct SectionHeader
    {
        public ulong TactKeyLookup;
        public int FileOffset;
        public int NumRecords;
        public int StringTableSize;
        public int CopyTableSize;
        public int SparseTableOffset; // CatalogDataOffset, absolute value, {uint offset, ushort size}[MaxId - MinId + 1]
        public int IndexDataSize; // int indexData[IndexDataSize / 4]
        public int ParentLookupDataSize; // uint NumRecords, uint minId, uint maxId, {uint id, uint index}[NumRecords], questionable usefulness...
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct SectionHeaderWDC3
    {
        public ulong TactKeyLookup;
        public int FileOffset;
        public int NumRecords;
        public int StringTableSize;
        public int OffsetRecordsEndOffset; // CatalogDataOffset, absolute value, {uint offset, ushort size}[MaxId - MinId + 1]
        public int IndexDataSize; // int indexData[IndexDataSize / 4]
        public int ParentLookupDataSize; // uint NumRecords, uint minId, uint maxId, {uint id, uint index}[NumRecords], questionable usefulness...
        public int OffsetMapIDCount;
        public int CopyTableCount;
    }

}
