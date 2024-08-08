#pragma warning disable CS0169

namespace DBCD.IO.Common
{
    public interface IHotfixEntry
    {
        int PushId { get; }
        int DataSize { get; }
        uint TableHash { get; }
        int RecordId { get; }
        bool IsValid { get; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct HotfixEntryV1 : IHotfixEntry
    {
        public int PushId { get; }
        public int DataSize { get; }
        public uint TableHash { get; }
        public int RecordId { get; }
        public bool IsValid { get; }

        private readonly byte pad1, pad2, pad3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct HotfixEntryV2 : IHotfixEntry
    {
        public uint Version { get; }
        public int PushId { get; }
        public int DataSize { get; }
        public uint TableHash { get; }
        public int RecordId { get; }
        public bool IsValid { get; }

        private readonly byte pad1, pad2, pad3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct HotfixEntryV7 : IHotfixEntry
    {
        public int PushId { get; }
        public uint TableHash { get; }
        public int RecordId { get; }
        public int DataSize { get; }
        public bool IsValid => op == 1;

        private readonly byte op, pad1, pad2, pad3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct HotfixEntryV8 : IHotfixEntry
    {
        public int PushId { get; }
        public int UniqueId { get; }
        public uint TableHash { get; }
        public int RecordId { get; }
        public int DataSize { get; }
        public bool IsValid => op == 1;

        private readonly byte op, pad1, pad2, pad3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct HotfixEntryV9 : IHotfixEntry
    {
        public int RegionId { get; }
        public int PushId { get; }
        public int UniqueId { get; }
        public uint TableHash { get; }
        public int RecordId { get; }
        public int DataSize { get; }
        public bool IsValid => op == 1;

        private readonly byte op, pad1, pad2, pad3;
    }
}
