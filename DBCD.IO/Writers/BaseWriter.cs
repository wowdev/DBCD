using DBCD.IO.Common;
using DBCD.IO.Readers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBCD.IO.Writers
{
    abstract class BaseWriter<T> where T : class
    {
        private static readonly Value32Comparer Value32Comparer = new Value32Comparer();

        public FieldCache<T>[] FieldCache { get; protected set; }
        public int RecordsCount { get; protected set; }
        public int StringTableSize { get; set; }
        public int FieldsCount { get; }
        public int RecordSize { get; set;  }
        public int IdFieldIndex { get; }
        public DB2Flags Flags { get; }
        public int PackedDataOffset { get; set; }

        #region Data
        public FieldMetaData[] Meta { get; protected set; }
        public ColumnMetaData[] ColumnMeta { get; protected set; }
        public OrderedHashSet<Value32[]>[] PalletData { get; protected set; }
        public Dictionary<int, Value32>[] CommonData { get; protected set; }
        public Dictionary<string, int> StringTable { get; protected set; }
        public SortedDictionary<int, int> CopyData { get; protected set; }
        public List<int> ReferenceData { get; protected set; }
        #endregion

        public BaseWriter(BaseReader reader)
        {
            FieldCache = typeof(T).ToFieldCache<T>();

            FieldsCount = reader.FieldsCount;
            RecordSize = reader.RecordSize;
            IdFieldIndex = reader.IdFieldIndex;
            Flags = reader.Flags;

            StringTable = new Dictionary<string, int>();
            CopyData = new SortedDictionary<int, int>();
            Meta = reader.Meta;
            ColumnMeta = reader.ColumnMeta;

            if (ColumnMeta != null)
            {
                CommonData = new Dictionary<int, Value32>[ColumnMeta.Length];
                PalletData = new OrderedHashSet<Value32[]>[ColumnMeta.Length];
                ReferenceData = new List<int>();
                // create the lookup collections
                for (int i = 0; i < ColumnMeta.Length; i++)
                {
                    CommonData[i] = new Dictionary<int, Value32>();
                    PalletData[i] = new OrderedHashSet<Value32[]>(Value32Comparer);
                }
            }

            // add an empty string at the first index
            InternString("");
        }            

        #region Methods

        public int InternString(string value)
        {
            if (StringTable.TryGetValue(value, out int index))
                return index;

            StringTable.Add(value, StringTableSize);

            int offset = StringTableSize;
            StringTableSize += Encoding.UTF8.GetByteCount(value) + 1;
            return offset;
        }

        public void WriteOffsetRecords(BinaryWriter writer, IDBRowSerializer<T> serializer, uint recordOffset, int sparseCount)
        {
            var sparseIdLookup = new Dictionary<int, uint>(sparseCount);

            for (int i = 0; i < sparseCount; i++)
            {
                if (serializer.Records.TryGetValue(i, out var record))
                {
                    if (CopyData.TryGetValue(i, out int copyid))
                    {
                        // copy records use their parent's offset
                        writer.Write(sparseIdLookup[copyid]);
                        writer.Write(record.TotalBytesWrittenOut);
                    }
                    else
                    {
                        writer.Write(sparseIdLookup[i] = recordOffset);
                        writer.Write(record.TotalBytesWrittenOut);
                        recordOffset += (uint)record.TotalBytesWrittenOut;
                    }
                }
                else
                {
                    // unused ids are empty records
                    writer.BaseStream.Position += 6;
                }
            }
        }

        public void WriteSecondaryKeyData(BinaryWriter writer, IDictionary<int, T> storage, int sparseCount)
        {
            // this was always the final field of wmominimaptexture.db2
            var fieldInfo = FieldCache[FieldCache.Length - 1];
            for (int i = 0; i < sparseCount; i++)
            {
                if (storage.TryGetValue(i, out var record))
                    writer.Write((int)fieldInfo.Getter(record));
                else
                    writer.BaseStream.Position += 4;
            }
        }

        public void HandleCompression(IDictionary<int, T> storage)
        {
            var externalCompressions = new HashSet<CompressionType>(new[] { CompressionType.None, CompressionType.Common });
            var valueComparer = new Value32Comparer();
            var indexFieldOffset = 0;
            var bitpackedOffset = 0;

            RecordSize = 0;
            PackedDataOffset = -1;

            for (int i = 0; i < FieldCache.Length; i++)
            {
                FieldCache<T> info = FieldCache[i];

                if (i == IdFieldIndex && Flags.HasFlagExt(DB2Flags.Index))
                {
                    indexFieldOffset++;
                    continue;
                }

                int fieldIndex = i - indexFieldOffset;

                if (fieldIndex >= ColumnMeta.Length)
                    break;

                var meta = ColumnMeta[fieldIndex];
                var compressionType = meta.CompressionType;
                int compressionSize = meta.Immediate.BitWidth;

                var newCompressedSize = compressionSize;

                var palletData = new ConcurrentBag<Value32[]>();
                
                if (!externalCompressions.Contains(compressionType))
                {
                    if (PackedDataOffset == -1)
                        PackedDataOffset = ((meta.RecordOffset + 8 - 1) / 8);
                }

                switch (compressionType)
                {
                    case CompressionType.SignedImmediate:
                        {
                            var largestMSB = storage.Values.Count switch
                            {
                                0 => 0,
                                _ => storage.Values.AsParallel().Max(row =>
                                {
                                    var value32 = Value32.Create(info.Getter(row));
                                    return value32.GetValue<int>().MostSignificantBit();
                                }),
                            };

                            newCompressedSize = largestMSB + 1;
                            break;
                        }
                    case CompressionType.Immediate:
                        {
                            if(info.FieldType == typeof(float))
                                newCompressedSize = 32;
                            else
                            {
                                if ((meta.Immediate.Flags & 0x1) == 0x1)
                                {
                                    var largestMSB = storage.Values.Count switch
                                    {
                                        0 => 0,
                                        _ => storage.Values.AsParallel().Max(row =>
                                        {
                                            var value32 = Value32.Create(info.Getter(row));
                                            return value32.GetValue<int>().MostSignificantBit();
                                        }),
                                    };

                                    newCompressedSize = largestMSB + 1;
                                }
                                else
                                {
                                    var maxValue = storage.Values.Count switch
                                    {
                                        0 => 0U,
                                        _ => storage.Values.AsParallel().Max(row =>
                                        {
                                            var value32 = Value32.Create(info.Getter(row));
                                            return value32.GetValue<uint>();
                                        }),
                                    };

                                    newCompressedSize = maxValue.MostSignificantBit();
                                }
                            }
                            break;
                        }
                    case CompressionType.Pallet:
                        {
                            Parallel.ForEach(storage.Values, row => palletData.Add(new[] { Value32.Create(info.Getter(row)) }));
                            var fieldMaxSize = palletData.AsParallel().Distinct(valueComparer).Count();
                            newCompressedSize = fieldMaxSize.MostSignificantBit();
                            break;
                        }
                    case CompressionType.PalletArray:
                        {
                            Parallel.ForEach(storage.Values, row =>
                            {
                                var baseArray = (Array)info.Getter(row);
                                Value32[] array = new Value32[baseArray.Length];
                                for (int i = 0; i < baseArray.Length; i++)
                                    array[i] = Value32.Create(baseArray.GetValue(i));
                                palletData.Add(array);
                            });

                            var fieldMaxSize = palletData.AsParallel().Distinct(valueComparer).Count();
                            newCompressedSize = fieldMaxSize.MostSignificantBit();
                            break;
                        }
                    case CompressionType.Common:
                        ColumnMeta[fieldIndex].Size = 0;
                        break;
                    case CompressionType.None:
                        break;
                    default:
                        throw new NotImplementedException("This compression type is not yet supported");
                }

                if (!externalCompressions.Contains(compressionType))
                {
                    ColumnMeta[fieldIndex].Immediate.BitWidth = ColumnMeta[fieldIndex].Size = (ushort)(newCompressedSize);
                    ColumnMeta[fieldIndex].Immediate.BitOffset = bitpackedOffset;
                    ColumnMeta[fieldIndex].RecordOffset = (ushort) RecordSize;

                    RecordSize += ColumnMeta[fieldIndex].Size;

                    bitpackedOffset += ColumnMeta[fieldIndex].Immediate.BitWidth;
                }
                else
                {
                    ColumnMeta[fieldIndex].RecordOffset = (ushort)RecordSize;
                    RecordSize += ColumnMeta[fieldIndex].Size;
                }

            }

            PackedDataOffset = Math.Max(0, PackedDataOffset);

            // TODO: Review how Blizzard handles this. This behavior matches a lot of the original DB2s, but not all. Maybe some math needs doing to make sure we're on 4 byte boundaries?
            RecordSize = ((RecordSize + 8 - 1) / 8);
        }

        #endregion
    }
}
