using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Represents a cached compiled prefab entry containing the compiled assembly metadata for a specific movie, variant, and fingerprint.
/// </summary>
public sealed record PrefabCacheEntry(string Movie, string Variant, string Fingerprint, string Sha256, IReadOnlyList<string> Dependencies)
{
    /// <summary>Gets the unique lookup key constructed from the movie, variant, and fingerprint.</summary>
    public string Key => KeyOf(Movie, Variant, Fingerprint);

    /// <summary>Constructs a composite cache key from the specified movie name, variant identifier, and fingerprint hash.</summary>
    public static string KeyOf(string movie, string variant, string fingerprint) => movie + "\n" + variant + "\n" + fingerprint;
}

/// <summary>Represents the deserialized contents of a prefab cache archive, including its generation key and entries.</summary>
public sealed record PrefabCacheContents(string? Generation, IReadOnlyList<PrefabCacheEntry> Entries);

/// <summary>
/// Manages the persistent disk cache for compiled prefab assemblies stored within a consolidated zip archive (<see cref="FileName"/>).
/// <para>
/// Persists compiled assemblies alongside cryptographic fingerprints and dependency manifests. Reads cached builds at startup,
/// writes updates atomically using temporary files, and queries read-only seed cache archives supplied by modules.
/// </para>
/// <para>
/// Thread-safe for background worker invocation. Diagnostics and generated source dumps are maintained in separate directories.
/// </para>
/// </summary>
public sealed class PrefabCache
{
    /// <summary>The file name of the primary compiled prefabs zip archive.</summary>
    public const string FileName = "CompiledPrefabs.zip";

    /// <summary>The directory name where compilation failure diagnostic reports are written.</summary>
    public const string FailedDirectoryName = "Failed";

    /// <summary>The directory name where dumped C# source files are written.</summary>
    public const string SourcesDirectoryName = "Sources";

    /// <summary>Legacy file prefix used by earlier per-assembly cache formats.</summary>
    private const string LegacyFilePrefix = CompiledPrefabManager.GeneratedNamespace + ".";

    /// <summary>Legacy generation file name used by earlier cache formats.</summary>
    private const string LegacyGenerationFileName = "generation.txt";

    private readonly object _lock = new();
    private readonly object _flushLock = new();
    private readonly string? _generation;
    private readonly Dictionary<string, PrefabCacheEntry> _local = new(StringComparer.Ordinal);
    /// <summary>Builds put since the last successful write, by <see cref="PrefabCacheEntry.Key"/>. Always a subset of <see cref="_local"/>.</summary>
    private readonly Dictionary<string, byte[]> _unwritten = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string File, PrefabCacheEntry Entry)> _seeded = new(StringComparer.Ordinal);
    private bool _dirty;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrefabCache"/> class and opens the cache archive in the specified directory.
    /// Purges outdated cache files from prior generations or legacy formats.
    /// </summary>
    public PrefabCache(string directory, string? generation, IEnumerable<string> seedFiles)
    {
        DirectoryPath = directory;
        FilePath = Path.Combine(directory, FileName);
        _generation = generation;

        Directory.CreateDirectory(directory);
        DeleteLeftovers();
        OpenLocal();
        foreach (var seed in seedFiles)
            OpenSeed(seed);
    }

    /// <summary>Gets the directory path where the cache archive and diagnostic files are stored.</summary>
    public string DirectoryPath { get; }

    /// <summary>Gets the full file path of the primary cache archive.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Computes the archive entry path for the specified movie and variant pair (<c>&lt;movie&gt;/&lt;variant&gt;</c>).
    /// </summary>
    public static string GetPairPath(string movie, string variant) => Sanitize(movie) + "/" + Sanitize(variant);

    /// <summary>Gets all local cache entries, including pending unwritten builds.</summary>
    public IReadOnlyList<PrefabCacheEntry> Builds
    {
        get { lock (_lock) return [.. _local.Values]; }
    }

    /// <summary>Finds a local cache entry matching the specified movie and variant regardless of fingerprint.</summary>
    public PrefabCacheEntry? FindBuildOf(string movie, string variant)
    {
        lock (_lock)
            return _local.Values.FirstOrDefault(x => x.Movie == movie && x.Variant == variant);
    }

    /// <summary>
    /// Attempts to read the compiled assembly bytes and cache entry for the specified movie, variant, and fingerprint.
    /// Evaluates the <paramref name="usable"/> predicate to verify dependency validity before returning.
    /// </summary>
    public (PrefabCacheEntry Entry, byte[] Assembly)? TryRead(string movie, string variant, string fingerprint, Func<PrefabCacheEntry, bool> usable)
    {
        var key = PrefabCacheEntry.KeyOf(movie, variant, fingerprint);
        PrefabCacheEntry? local;
        byte[]? unwritten;
        (string File, PrefabCacheEntry Entry) seed;
        bool seeded;
        lock (_lock)
        {
            _unwritten.TryGetValue(key, out unwritten);
            _local.TryGetValue(key, out local);
            seeded = _seeded.TryGetValue(key, out seed);
        }

        if (local is not null && usable(local) && (unwritten ?? ReadAssembly(FilePath, local)) is { } fromLocal)
            return (local, fromLocal);
        if (seeded && usable(seed.Entry) && ReadAssembly(seed.File, seed.Entry) is { } fromSeed)
            return (seed.Entry, fromSeed);
        return null;
    }

    /// <summary>
    /// Executes the specified delegate for each cached build across the local archive and seed archives.
    /// </summary>
    public void ForEachBuild(Action<PrefabCacheEntry, byte[]> action) => ForEachBuild(static _ => true, action);

    /// <summary>
    /// Executes the specified delegate for each cached build accepted by <paramref name="include"/>, across the local archive
    /// and seed archives. A build that is not included is not read.
    /// </summary>
    public void ForEachBuild(Func<PrefabCacheEntry, bool> include, Action<PrefabCacheEntry, byte[]> action)
    {
        List<PrefabCacheEntry> local;
        Dictionary<string, byte[]> unwritten;
        List<IGrouping<string, PrefabCacheEntry>> seeds;
        lock (_lock)
        {
            local = [.. _local.Values];
            unwritten = new(_unwritten, StringComparer.Ordinal);
            seeds = [.. _seeded.Values.Where(x => !_local.ContainsKey(x.Entry.Key)).GroupBy(x => x.File, x => x.Entry, StringComparer.OrdinalIgnoreCase)];
        }

        // Outside the lock: the predicate may be slow (dependency checks)
        local = [.. local.Where(include)];
        seeds = [.. seeds.SelectMany(x => x.Where(include).Select(y => (File: x.Key, Entry: y))).GroupBy(x => x.File, x => x.Entry, StringComparer.OrdinalIgnoreCase)];

        foreach (var entry in local.Where(x => unwritten.ContainsKey(x.Key)))
            action(entry, unwritten[entry.Key]);

        ForEachBuild(FilePath, local.Where(x => !unwritten.ContainsKey(x.Key)).ToList(), action);
        foreach (var seed in seeds)
            ForEachBuild(seed.Key, [.. seed], action);
    }

    /// <summary>Adds or updates a compiled build in the cache, queuing it for serialization during the next flush.</summary>
    public void Put(string movie, string variant, string fingerprint, byte[] assembly, IReadOnlyList<string>? dependencies = null)
    {
        var entry = new PrefabCacheEntry(movie, variant, fingerprint, Sha256(assembly), dependencies ?? []);
        lock (_lock)
        {
            RemoveOtherBuildsCore(movie, variant, fingerprint);
            _local[entry.Key] = entry;
            _unwritten[entry.Key] = assembly;
            _dirty = true;
        }
    }

    /// <summary>
    /// Removes existing local builds of the specified pair if their fingerprint does not match <paramref name="fingerprint"/>.
    /// </summary>
    /// <returns><see langword="true"/> if outdated builds were removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveOtherBuilds(string movie, string variant, string fingerprint)
    {
        lock (_lock)
        {
            if (!RemoveOtherBuildsCore(movie, variant, fingerprint))
                return false;
            _dirty = true;
            return true;
        }
    }

    private bool RemoveOtherBuildsCore(string movie, string variant, string fingerprint)
    {
        var stale = _local.Values.Where(x => x.Movie == movie && x.Variant == variant && x.Fingerprint != fingerprint).ToList();
        foreach (var entry in stale)
        {
            _local.Remove(entry.Key);
            _unwritten.Remove(entry.Key);
        }
        return stale.Count > 0;
    }

    /// <summary>
    /// Flushes pending cache additions and removals to disk by rewriting the zip archive. Coalesces concurrent flush calls.
    /// </summary>
    public void Flush()
    {
        while (true)
        {
            if (!Monitor.TryEnter(_flushLock))
                return;
            try
            {
                while (TakeChanges(out var entries, out var unwritten))
                {
                    if (!TryWrite(entries, unwritten))
                        return;
                }
            }
            finally
            {
                Monitor.Exit(_flushLock);
            }

            // Changed by a caller that found the lock taken just before it was released
            lock (_lock)
            {
                if (!_dirty)
                    return;
            }
        }
    }

    private bool TakeChanges(out List<PrefabCacheEntry> entries, out Dictionary<string, byte[]> unwritten)
    {
        lock (_lock)
        {
            entries = [.. _local.Values];
            unwritten = new(_unwritten, StringComparer.Ordinal);
            if (!_dirty)
                return false;
            _dirty = false;
            return true;
        }
    }

    private bool TryWrite(List<PrefabCacheEntry> entries, Dictionary<string, byte[]> unwritten)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var written = WriteArchive(entries, unwritten);
            lock (_lock)
            {
                // A build whose bytes could not be copied from the old archive is gone with it
                foreach (var entry in entries.Where(x => !written.Contains(x)))
                {
                    if (_local.TryGetValue(entry.Key, out var current) && ReferenceEquals(current, entry))
                        _local.Remove(entry.Key);
                }
                foreach (var pair in unwritten)
                {
                    if (_unwritten.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, pair.Value))
                        _unwritten.Remove(pair.Key);
                }
            }
            Trace.TraceInformation("UIExtenderEx: wrote {0} compiled prefabs to '{1}' in {2} ms", written.Count, FilePath, stopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (Exception e)
        {
            lock (_lock)
                _dirty = true;
            Trace.TraceWarning("UIExtenderEx: failed to write the compiled prefab cache '{0}': {1}", FilePath, e.Message);
            return false;
        }
    }

    private HashSet<PrefabCacheEntry> WriteArchive(List<PrefabCacheEntry> entries, Dictionary<string, byte[]> unwritten)
    {
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            HashSet<PrefabCacheEntry> written;
            using (var previous = TryOpenRead(FilePath))
                written = PrefabCacheArchive.Write(temporary, _generation, entries, x => unwritten.TryGetValue(x.Key, out var bytes) ? bytes : previous is null ? null : PrefabCacheArchive.ReadAssembly(previous, x));
            Commit(temporary, FilePath);
            return written;
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private void OpenLocal()
    {
        PrefabCacheContents? contents = null;
        string? problem = null;
        if (File.Exists(FilePath))
        {
            try
            {
                using var archive = OpenRead(FilePath);
                contents = PrefabCacheArchive.Read(archive);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                problem = e.Message;
            }
        }

        if (contents is not null && (_generation is null || contents.Generation == _generation))
        {
            foreach (var entry in contents.Entries)
                _local[entry.Key] = entry;
            return;
        }

        if (contents is null && problem is null && _generation is null)
            return;

        // Every fingerprint starts with the generation, so nothing in an archive of another one could match again
        if (contents is not null)
            Trace.TraceInformation("UIExtenderEx: the compiled prefab cache is from another UIExtenderEx or game build, starting over");
        else if (problem is not null)
            Trace.TraceWarning("UIExtenderEx: the compiled prefab cache '{0}' cannot be read, starting over: {1}", FilePath, problem);
        StartOver();
    }

    /// <summary>
    /// Resets the local cache directory, deleting reports and writing a clean empty archive for the current generation.
    /// </summary>
    private void StartOver()
    {
        TryDeleteDirectory(Path.Combine(DirectoryPath, FailedDirectoryName));
        TryDeleteDirectory(Path.Combine(DirectoryPath, SourcesDirectoryName));
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            PrefabCacheArchive.Write(temporary, _generation, [], _ => null);
            Commit(temporary, FilePath);
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: could not reset the compiled prefab cache '{0}': {1}", FilePath, e.Message);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private void OpenSeed(string file)
    {
        try
        {
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase))
                return;

            using var archive = OpenRead(file);
            var contents = PrefabCacheArchive.Read(archive);
            if (_generation is not null && contents.Generation != _generation)
            {
                Trace.TraceInformation("UIExtenderEx: the compiled prefab seed '{0}' is for another UIExtenderEx or game build, ignoring it", file);
                return;
            }

            foreach (var entry in contents.Entries.Where(x => !_seeded.ContainsKey(x.Key)))
                _seeded[entry.Key] = (file, entry);
            Trace.TraceInformation("UIExtenderEx: using the compiled prefab seed '{0}' with {1} builds", file, contents.Entries.Count);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Trace.TraceWarning("UIExtenderEx: the compiled prefab seed '{0}' cannot be read, ignoring it: {1}", file, e.Message);
        }
    }

    /// <summary>
    /// Removes legacy loose files and incomplete temporary files left by interrupted write operations.
    /// </summary>
    private void DeleteLeftovers()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(DirectoryPath))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(LegacyFilePrefix, StringComparison.Ordinal) || name == LegacyGenerationFileName)
                    TryDelete(file);
                else if (name.StartsWith(FileName + ".", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal)
                         && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > TimeSpan.FromMinutes(1))
                    TryDelete(file);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("UIExtenderEx: could not clean the compiled prefab cache folder: {0}", e.Message);
        }
    }

    private static void ForEachBuild(string file, IReadOnlyList<PrefabCacheEntry> entries, Action<PrefabCacheEntry, byte[]> action)
    {
        if (entries.Count == 0)
            return;

        List<(PrefabCacheEntry Entry, byte[] Bytes)> builds = [];
        try
        {
            using var archive = OpenRead(file);
            foreach (var entry in entries)
            {
                if (PrefabCacheArchive.ReadAssembly(archive, entry) is { } bytes)
                    builds.Add((entry, bytes));
                else
                    Trace.TraceWarning("UIExtenderEx: the cached build of '{0}' for '{1}' in '{2}' is missing or damaged", entry.Movie, entry.Variant, file);
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("UIExtenderEx: could not read the compiled prefab cache '{0}': {1}", file, e.Message);
        }

        foreach (var build in builds)
            action(build.Entry, build.Bytes);
    }

    private static byte[]? ReadAssembly(string file, PrefabCacheEntry entry)
    {
        try
        {
            using var archive = OpenRead(file);
            return PrefabCacheArchive.ReadAssembly(archive, entry);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("UIExtenderEx: could not read the build of '{0}' for '{1}' from '{2}': {3}", entry.Movie, entry.Variant, file, e.Message);
            return null;
        }
    }

    /// <summary>
    /// Opens a read-only <see cref="ZipArchive"/> stream allowing concurrent reads and file deletion.
    /// </summary>
    private static ZipArchive OpenRead(string file) =>
        new(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete), ZipArchiveMode.Read);

    /// <summary>
    /// Attempts to open the specified cache archive file for reading, returning <see langword="null"/> if the file is missing or corrupt.
    /// </summary>
    private static ZipArchive? TryOpenRead(string file)
    {
        if (!File.Exists(file))
            return null;
        try
        {
            return OpenRead(file);
        }
        catch (InvalidDataException e)
        {
            Trace.TraceWarning("UIExtenderEx: the compiled prefab cache '{0}' is damaged, only the builds of this session are kept: {1}", file, e.Message);
            return null;
        }
    }

    /// <summary>Replaces the destination archive atomically with the completed temporary file.</summary>
    private static void Commit(string temporary, string file)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(file))
                    File.Replace(temporary, file, null, ignoreMetadataErrors: true);
                else
                    File.Move(temporary, file);
                return;
            }
            catch (Exception e) when (attempt < 3 && e is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file))
                File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("UIExtenderEx: could not delete '{0}': {1}", file, e.Message);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("UIExtenderEx: could not delete '{0}': {1}", directory, e.Message);
        }
    }

    /// <summary>Sanitizes file and path segment characters incompatible with file systems by replacing them with underscores.</summary>
    private static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(c < 32 || "\"<>|:*?\\/".IndexOf(c) >= 0 ? '_' : c);
        return sb.ToString();
    }

    /// <summary>Computes the lowercase hexadecimal SHA-256 hash of the specified byte array.</summary>
    public static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

/// <summary>
/// Handles serialization and deserialization of the zip-based compiled prefab cache archive format (version 3).
/// </summary>
public static class PrefabCacheArchive
{
    /// <summary>The current archive format schema version.</summary>
    public const int FormatVersion = 3;

    /// <summary>The zip entry name for the archive manifest.</summary>
    public const string ManifestEntryName = "cache.txt";

    /// <summary>The file extension applied to compiled assembly zip entries.</summary>
    public const string AssemblyExtension = ".dll";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Reads manifest metadata from the specified archive file without loading assembly binaries.</summary>
    public static PrefabCacheContents Read(string file)
    {
        using var archive = new ZipArchive(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete), ZipArchiveMode.Read);
        return Read(archive);
    }

    /// <summary>Reads manifest metadata from the provided <see cref="ZipArchive"/> instance.</summary>
    public static PrefabCacheContents Read(ZipArchive archive)
    {
        var blocks = archive.GetEntry(ManifestEntryName) is { } manifest
            ? ReadText(manifest).Replace("\r\n", "\n").Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries).ToList()
            : throw new InvalidDataException($"'{ManifestEntryName}' is missing, this is not a compiled prefab cache");
        var header = blocks.Count > 0 ? ParseFields(blocks[0]) : new Dictionary<string, string>();
        if (!header.TryGetValue("format", out var format) || format != FormatVersion.ToString())
            throw new InvalidDataException($"format '{format}' is not {FormatVersion}");

        var entries = new List<PrefabCacheEntry>();
        foreach (var block in blocks.Skip(1))
        {
            var fields = ParseFields(block);
            if (!fields.TryGetValue("movie", out var movie) || !fields.TryGetValue("variant", out var variant)
                || !fields.TryGetValue("fingerprint", out var fingerprint) || !fields.TryGetValue("sha256", out var sha256))
                continue;

            var path = PrefabCache.GetPairPath(movie, variant);
            if (archive.GetEntry(path + AssemblyExtension) is null)
                continue;
            entries.Add(new(movie, variant, fingerprint, sha256, ParseAll(block, "dependency")));
        }

        header.TryGetValue("generation", out var generation);
        return new(generation, entries);
    }

    /// <summary>Extracts the assembly byte array for the specified cache entry from the archive, validating its SHA-256 hash.</summary>
    public static byte[]? ReadAssembly(ZipArchive archive, PrefabCacheEntry entry)
    {
        if (archive.GetEntry(PrefabCache.GetPairPath(entry.Movie, entry.Variant) + AssemblyExtension) is not { } zipEntry)
            return null;

        using var stream = zipEntry.Open();
        using var memory = new MemoryStream(zipEntry.Length > 0 && zipEntry.Length < int.MaxValue ? (int) zipEntry.Length : 0);
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        return string.Equals(PrefabCache.Sha256(bytes), entry.Sha256, StringComparison.OrdinalIgnoreCase) ? bytes : null;
    }

    /// <summary>
    /// Serializes cache entries and assemblies into a new archive file.
    /// </summary>
    /// <returns>A set of entries successfully written to the archive.</returns>
    public static HashSet<PrefabCacheEntry> Write(string file, string? generation, IEnumerable<PrefabCacheEntry> entries, Func<PrefabCacheEntry, byte[]?> assembly)
    {
        var written = new HashSet<PrefabCacheEntry>();
        using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        var manifest = new StringBuilder(FormatFields(("format", FormatVersion.ToString()), ("generation", generation)));
        foreach (var entry in entries.OrderBy(x => x.Movie, StringComparer.Ordinal).ThenBy(x => x.Variant, StringComparer.Ordinal))
        {
            if (assembly(entry) is not { } bytes)
                continue;

            var path = PrefabCache.GetPairPath(entry.Movie, entry.Variant);
            using (var assemblyStream = archive.CreateEntry(path + AssemblyExtension, CompressionLevel.Fastest).Open())
                assemblyStream.Write(bytes, 0, bytes.Length);
            manifest.Append('\n').Append(FormatFields(("movie", entry.Movie), ("variant", entry.Variant), ("fingerprint", entry.Fingerprint), ("sha256", entry.Sha256)));
            foreach (var dependency in entry.Dependencies)
                manifest.Append("dependency=").Append(dependency).Append('\n');
            written.Add(entry);
        }
        WriteText(archive, ManifestEntryName, manifest.ToString());
        return written;
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Utf8);
        return reader.ReadToEnd();
    }

    private static void WriteText(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name, CompressionLevel.Fastest).Open(), Utf8);
        writer.Write(text);
    }

    private static Dictionary<string, string> ParseFields(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split(['\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=');
            if (separator > 0)
                fields[line.Substring(0, separator)] = line.Substring(separator + 1);
        }
        return fields;
    }

    /// <summary>Parses all values associated with a repeating key in a manifest block.</summary>
    private static List<string> ParseAll(string text, string key) =>
        [.. text.Split(['\n'], StringSplitOptions.RemoveEmptyEntries).Where(x => x.StartsWith(key + "=", StringComparison.Ordinal)).Select(x => x.Substring(key.Length + 1))];

    private static string FormatFields(params (string Key, string? Value)[] fields)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in fields)
        {
            if (value is not null)
                sb.Append(key).Append('=').Append(value).Append('\n');
        }
        return sb.ToString();
    }
}