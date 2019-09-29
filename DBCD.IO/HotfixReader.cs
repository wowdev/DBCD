using DBCD.IO.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DBCD.IO
{
    public class HotfixReader
    {
        private readonly HTFXReader _reader;

        #region Header

        public int Version => _reader.Version;
        public int BuildId => _reader.BuildId;

        #endregion

        public HotfixReader(string fileName) : this(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) { }

        public HotfixReader(Stream stream)
        {
            using (var bin = new BinaryReader(stream))
            {
                var identifier = new string(bin.ReadChars(4));
                stream.Position = 0;
                switch (identifier)
                {
                    case "XFTH":
                        _reader = new HTFXReader(stream);
                        break;
                    default:
                        throw new Exception("Hotfix type " + identifier + " is not supported!");
                }
            }
        }


        public void ApplyHotfixes<T>(IDictionary<int, T> storage, DBParser dbReader) where T : class, new() => ReadHotfixes(storage, dbReader);

        public void CombineCaches(params string[] files)
        {
            foreach (var file in files)
            {
                if (!File.Exists(file))
                    continue;

                // parse the new cache
                var reader = new HTFXReader(file);
                if (reader.BuildId != BuildId)
                    continue;

                // add additional hotfix entries
                _reader.Combine(reader);
            }
        }


        protected virtual void ReadHotfixes<T>(IDictionary<int, T> storage, DBParser dbReader) where T : class, new()
        {
            var fieldCache = typeof(T).ToFieldCache<T>();

            // Id fields need to be excluded if not inline
            if (dbReader.Flags.HasFlagExt(DB2Flags.Index))
                fieldCache[dbReader.IdFieldIndex].IndexMapField = true;

            // TODO verify hotfixes need to be applied sequentially
            var records = _reader.GetRecords(dbReader.TableHash).OrderBy(x => x.PushId);

            foreach (var row in records)
            {
                if (row.IsValid)
                {
                    T entry = new T();
                    row.GetFields(fieldCache, entry);
                    storage[row.RecordId] = entry;
                }
                else
                {
                    storage.Remove(row.RecordId);
                }
            }
        }
    }
}
