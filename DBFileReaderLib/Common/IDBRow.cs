namespace DBFileReaderLib.Common
{
    interface IDBRow
    {
        int Id { get; set; }
        BitReader Data { get; set; }
        void GetFields<T>(FieldCache<T>[] fields, T entry);
        IDBRow Clone();
    }
}
