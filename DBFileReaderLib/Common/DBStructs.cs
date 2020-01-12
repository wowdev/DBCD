using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS0649
#pragma warning disable IDE0044

namespace DBFileReaderLib.Common
{
    public interface IEncryptableDatabaseSection
    {
        ulong TactKeyLookup { get; }
        int NumRecords { get; }
    }

    public interface IEncryptionSupportingReader
    {
        List<IEncryptableDatabaseSection> GetEncryptedSections();
    }

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
        public Dictionary<int, int> Entries { get; set; } = new Dictionary<int, int>();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct SparseEntry
    {
        public uint Offset;
        public ushort Size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct SectionHeader : IEncryptableDatabaseSection
    {
        public ulong TactKeyLookup;
        public int FileOffset;
        public int NumRecords;
        public int StringTableSize;
        public int CopyTableSize;
        public int SparseTableOffset; // CatalogDataOffset, absolute value, {uint offset, ushort size}[MaxId - MinId + 1]
        public int IndexDataSize; // int indexData[IndexDataSize / 4]
        public int ParentLookupDataSize; // uint NumRecords, uint minId, uint maxId, {uint id, uint index}[NumRecords], questionable usefulness...

        ulong IEncryptableDatabaseSection.TactKeyLookup => this.TactKeyLookup;
        int IEncryptableDatabaseSection.NumRecords => this.NumRecords;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct SectionHeaderWDC3 : IEncryptableDatabaseSection
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

        ulong IEncryptableDatabaseSection.TactKeyLookup => this.TactKeyLookup;
        int IEncryptableDatabaseSection.NumRecords => this.NumRecords;
    }

}
