using DBCD.IO.Common;
using DBCD.IO.Readers;
using DBCD.IO.Writers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DBCD.IO
{
    public class DBParser
    {
        private readonly BaseReader _reader;

        #region Header
        public Type RecordType { get; protected set; }
        public string Identifier { get; }
        public int RecordsCount => _reader.RecordsCount;
        public int FieldsCount => _reader.FieldsCount;
        public int RecordSize => _reader.RecordSize;
        public int StringTableSize => _reader.StringTableSize;
        public uint TableHash => _reader.TableHash;
        public uint LayoutHash => _reader.LayoutHash;
        public int IdFieldIndex => _reader.IdFieldIndex;
        public DB2Flags Flags => _reader.Flags;
        #endregion

        public DBParser(string fileName) : this(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) { }

        public DBParser(Stream stream)
        {
            using (var bin = new BinaryReader(stream))
            {
                Identifier = new string(bin.ReadChars(4));
                stream.Position = 0;
                switch (Identifier)
                {
                    case "WDC5":
                        _reader = new WDC5Reader(stream);
                        break;
                    case "WDC4":
                        _reader = new WDC4Reader(stream);
                        break;
                    case "WDC3":
                        _reader = new WDC3Reader(stream);
                        break;
                    case "WDC2":
                    case "1SLC":
                        _reader = new WDC2Reader(stream);
                        break;
                    case "WDC1":
                        _reader = new WDC1Reader(stream);
                        break;
                    case "WDB6":
                        _reader = new WDB6Reader(stream);
                        break;
                    case "WDB5":
                        _reader = new WDB5Reader(stream);
                        break;
                    case "WDB4":
                        _reader = new WDB4Reader(stream);
                        break;
                    case "WDB3":
                        _reader = new WDB3Reader(stream);
                        break;
                    case "WDB2":
                        _reader = new WDB2Reader(stream);
                        break;
                    case "WDBC":
                        _reader = new WDBCReader(stream);
                        break;
                    default:
                        throw new Exception("DB type " + Identifier + " is not supported!");
                }
            }
        }

        /// <summary>
        /// Get all records for <see cref="Storage{T}"/>
        /// </summary>
        public Storage<T> GetRecords<T>() where T : class, new() => new Storage<T>(this);

        /// <summary>
        /// Populate the storage with values.
        /// </summary>
        public void PopulateRecords<T>(IDictionary<int, T> storage) where T : class, new() => ReadRecords(storage);

        protected virtual void ReadRecords<T>(IDictionary<int, T> storage) where T : class, new()
        {
            var fieldCache = (RecordType = typeof(T)).ToFieldCache<T>();

            _reader.Enumerate((row) =>
            {
                T entry = new T();
                row.GetFields(fieldCache, entry);
                lock (storage)
                    storage.Add(row.Id, entry);
            });
        }

        /// <summary>
        /// Get's all encrypted DB2 Sections.
        /// </summary>
        public Dictionary<ulong, int> GetEncryptedSections()
        {
            var reader = _reader as IEncryptionSupportingReader;

            if (reader == null || reader.GetEncryptedSections() == null)
            {
                return new Dictionary<ulong, int>();
            }

            return reader.GetEncryptedSections().Where(s => s != null).ToDictionary(s => s.TactKeyLookup, s => s.NumRecords);
        }

        public Dictionary<ulong, int[]> GetEncryptedIDs()
        {
            var reader = this._reader as IEncryptionSupportingReader;

            if (reader == null || reader.GetEncryptedIDs() == null)
            {
                return new Dictionary<ulong, int[]>();
            }

            return reader.GetEncryptedIDs();
        }

        /// <summary>
        /// Write records to a new .db2 file.
        /// </summary>
        public void WriteRecords<T>(IDictionary<int, T> storage, string filename) where T : class, new() =>
            WriteRecords(storage, File.Open(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite));

        /// <summary>
        /// Write records to a new .db2 file.
        /// </summary>
        public void WriteRecords<T>(IDictionary<int, T> storage, Stream stream) where T : class, new()
        {
            if (typeof(T) != RecordType)
                throw new FormatException($"Invalid record type, expected {RecordType.Name}");

            BaseWriter<T> writer;
            switch (Identifier)
            {
                case "WDC5":
                    writer = new WDC5Writer<T>((WDC5Reader)_reader, storage, stream);
                    break;
                case "WDC4":
                    writer = new WDC4Writer<T>((WDC4Reader)_reader, storage, stream);
                    break;
                case "WDC3":
                    writer = new WDC3Writer<T>((WDC3Reader)_reader, storage, stream);
                    break;
                case "WDC2":
                case "1SLC":
                    writer = new WDC2Writer<T>((WDC2Reader)_reader, storage, stream);
                    break;
                case "WDC1":
                    writer = new WDC1Writer<T>((WDC1Reader)_reader, storage, stream);
                    break;
                case "WDB6":
                    writer = new WDB6Writer<T>((WDB6Reader)_reader, storage, stream);
                    break;
                case "WDB5":
                    writer = new WDB5Writer<T>((WDB5Reader)_reader, storage, stream);
                    break;
                case "WDB4":
                    writer = new WDB4Writer<T>((WDB4Reader)_reader, storage, stream);
                    break;
                case "WDB3":
                    writer = new WDB3Writer<T>((WDB3Reader)_reader, storage, stream);
                    break;
                case "WDB2":
                    writer = new WDB2Writer<T>((WDB2Reader)_reader, storage, stream);
                    break;
                case "WDBC":
                    writer = new WDBCWriter<T>((WDBCReader)_reader, storage, stream);
                    break;
            }
        }

        /// <summary>
        /// Clears temporary data however prevents further <see cref="ReadRecords"/> calls
        /// </summary>
        public void ClearCache() => _reader.Clear();
    }
}