using DBFileReaderLib.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DBFileReaderLib
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


        public void ApplyHotfixes<T>(IDictionary<int, T> storage, DBReader dbReader) where T : class, new() => ReadHotfixes(storage, dbReader);

        public void CombineCaches(params string[] files)
        {
            foreach (var file in files)
            {
                CombineCache(file);
            }
        }
        
        public void CombineCache(string file)
        {
            if (!File.Exists(file))
                return;

            // parse the new cache
            var reader = new HTFXReader(file);
            if (reader.BuildId != BuildId)
                return;

            // add additional hotfix entries
            _reader.Combine(reader);
        }

        protected virtual void ReadHotfixes<T>(IDictionary<int, T> storage, DBReader dbReader) where T : class, new()
        {
            var fieldCache = typeof(T).GetFields().Select(x => new FieldCache<T>(x)).ToArray();

            // Id fields need to be excluded if not inline
            if (dbReader.Flags.HasFlagExt(DB2Flags.Index))
                fieldCache[dbReader.IdFieldIndex].IndexMapField = true;

            // TODO verify hotfixes need to be applied sequentially
            var records = _reader.GetRecords(dbReader.TableHash).OrderBy(x => x.PushId);

            // Check if there are any valid cached records with data, don't remove row if so. 
            // Example situation: Blizzard has invalidated TACTKey records in the same DBCache as valid ones.
            // Without the below check, valid cached TACTKey records would be removed by the invalidated records afterwards.
            // This only seems to be relevant for cached tables and specifically TACTKey, BroadcastText/ItemSparse only show up single times it seems.
            var shouldDelete = dbReader.TableHash != 3744420815 || !records.Any(r => r.IsValid && r.PushId == -1 && r.DataSize > 0);
            
            foreach (var row in records)
            {
                if (row.IsValid & row.DataSize > 0)
                {
                    T entry = new T();
                    row.GetFields(fieldCache, entry);
                    storage[row.RecordId] = entry;
                }
                else if(shouldDelete)
                {
                    storage.Remove(row.RecordId);
                }
            }
        }
    }
}
