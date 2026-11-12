using DBDefsLib;
using DBDefsLib.Constants;
using DBDefsLib.Structs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace DBCD.Providers
{
    /// <summary>
    /// Resolves enum/flag definitions from the WoWDBDefs GitHub repository,
    /// downloading the mapping file and individual enum/flag files on demand.
    /// </summary>
    public class GithubEnumProvider : IEnumProvider
    {
        private static readonly Uri BaseURI = new Uri("https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/meta/");
        private readonly HttpClient client = new HttpClient();

        private static bool UseCache = false;
        private static string CachePath { get; } = "EnumCache/";
        private static readonly TimeSpan CacheExpiryTime = new TimeSpan(1, 0, 0, 0);

        private readonly Dictionary<string, EnumDefinition> cache = new();

        public List<MappingDefinition> Mappings { get; }

        public GithubEnumProvider(bool useCache = false)
        {
            UseCache = useCache;
            client.BaseAddress = BaseURI;

            if (useCache && !Directory.Exists(CachePath))
                Directory.CreateDirectory(CachePath);

            var mappingStream = FetchFile("mapping.dbdm");
            Mappings = new DBDMReader().Read(mappingStream);
            PopulateCache();
        }

        public EnumDefinition? GetEnumDefinition(string tableName, string columnName, int? arrayIndex = null,
            string conditionalTable = null, string conditionalColumn = null, string conditionalValue = null)
        {
            // Build base key (with or without array index)
            var baseKey = arrayIndex.HasValue
                ? $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}[{arrayIndex}]"
                : $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}";

            // If conditional context supplied, try conditional key first
            if (!string.IsNullOrEmpty(conditionalTable))
            {
                var conditionalKey = $"{baseKey}@{conditionalTable.ToLowerInvariant()}.{conditionalColumn!.ToLowerInvariant()}={conditionalValue}";
                if (cache.TryGetValue(conditionalKey, out var conditional))
                    return conditional;
            }

            // Fall back to unconditional (handles arrayIndex fallback too)
            if (arrayIndex.HasValue)
            {
                if (cache.TryGetValue(baseKey, out var specific))
                    return specific;

                var fallbackKey = $"{tableName.ToLowerInvariant()}::{columnName.ToLowerInvariant()}";
                return cache.TryGetValue(fallbackKey, out var fallback) ? fallback : null;
            }

            return cache.TryGetValue(baseKey, out var cached) ? cached : null;
        }

        private void PopulateCache()
        {
            // Deduplicate file fetches: multiple mappings may point to the same enum/flag file.
            var fileCache = new Dictionary<string, EnumDefinition?>();

            foreach (var mapping in Mappings)
            {
                if (mapping.meta is MetaType.COLOR)
                    continue;

                var cacheKey = BuildCacheKey(mapping);
                var fileKey = $"{mapping.meta}::{mapping.metaValue}";
                if (!fileCache.TryGetValue(fileKey, out var enumDef))
                {
                    enumDef = TryReadEnumFile(mapping);
                    fileCache[fileKey] = enumDef;
                }

                if (enumDef.HasValue)
                    cache[cacheKey] = enumDef.Value;
            }
        }

        private EnumDefinition? TryReadEnumFile(MappingDefinition mapping)
        {
            var dir = mapping.meta == MetaType.ENUM ? "enums" : "flags";
            var ext = mapping.meta == MetaType.ENUM ? ".dbde" : ".dbdf";
            var query = $"{dir}/{mapping.metaValue}{ext}";

            try
            {
                var stream = FetchFile(query);
                return new DBDEnumReader().Read(stream, mapping.meta);
            }
            catch
            {
                return null;
            }
        }

        private Stream FetchFile(string query)
        {
            if (UseCache)
            {
                var cacheFile = Path.Combine(CachePath, query.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(cacheFile))
                {
                    var lastWrite = File.GetLastWriteTime(cacheFile);
                    if (DateTime.Now - lastWrite < CacheExpiryTime)
#if NET11_0_OR_GREATER
                        return new ReadOnlyMemoryStream(File.ReadAllBytes(cacheFile));
#else
                        return new MemoryStream(File.ReadAllBytes(cacheFile));
#endif
                }
            }

            var bytes = client.GetByteArrayAsync(query).Result;

            if (UseCache)
            {
                var cacheFile = Path.Combine(CachePath, query.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
                File.WriteAllBytes(cacheFile, bytes);
            }

            return new MemoryStream(bytes);
        }

        private static string BuildCacheKey(MappingDefinition m)
        {
            var key = m.arrIndex.HasValue
                ? $"{m.tableName.ToLowerInvariant()}::{m.columnName.ToLowerInvariant()}[{m.arrIndex}]"
                : $"{m.tableName.ToLowerInvariant()}::{m.columnName.ToLowerInvariant()}";

            if (!string.IsNullOrEmpty(m.conditionalTable))
                key += $"@{m.conditionalTable.ToLowerInvariant()}.{m.conditionalColumn.ToLowerInvariant()}={m.conditionalValue}";

            return key;
        }
    }
}
