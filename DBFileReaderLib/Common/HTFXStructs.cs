#pragma warning disable CS0169

namespace DBFileReaderLib.Common
{
    interface IHotfixEntry
    {
        int PushId { get; }
        int DataSize { get; }
        uint TableHash { get; }
        int RecordId { get; }
        bool IsValid { get; }
    }

    struct HotfixEntryV1 : IHotfixEntry
    {
        public int PushId { get; }
        public int DataSize { get; }
        public uint TableHash { get; }
        public int RecordId { get; }
        public bool IsValid { get; }

        private readonly byte pad1, pad2, pad3;
    }

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

    struct HotfixEntryV7 : IHotfixEntry
    {
        public int PushId { get; }
        public uint TableHash { get; }
        public int RecordId { get; }
        public int DataSize { get; }
        public bool IsValid { get; }

        private readonly byte pad1, pad2, pad3;
    }
}
