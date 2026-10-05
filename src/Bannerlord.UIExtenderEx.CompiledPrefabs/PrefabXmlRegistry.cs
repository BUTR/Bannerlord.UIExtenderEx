using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Maintains SHA-256 hashes of patched prefab XML documents to detect document mutations and invalidate outdated cached assemblies.
/// </summary>
public static class PrefabXmlRegistry
{
    private static readonly ConcurrentDictionary<string, string> Hashes = new(StringComparer.Ordinal);
    private static ConditionalWeakTable<WidgetPrefab, RecordedXml> ParsedHashes = new();
    private sealed record RecordedXml(string Hash);
    private static int _version;

    /// <summary>
    /// Gets the monotonic registry version counter. Increments when custom prefabs are dynamically registered or when the registry is cleared.
    /// </summary>
    public static int Version => Volatile.Read(ref _version);

    /// <summary>Increments the monotonic registry version counter.</summary>
    public static void Touch() => Interlocked.Increment(ref _version);

    /// <summary>Computes and records the SHA-256 hash for the specified prefab XML document.</summary>
    public static void Record(string prefabName, XmlDocument document)
        => Record(prefabName, Hash(document.OuterXml));

    private static void Record(string prefabName, string hash)
    {
        if (string.IsNullOrEmpty(prefabName))
            return;

        if (Hashes.TryGetValue(prefabName, out var previous) && previous == hash)
            return;

        Hashes[prefabName] = hash;
        // The same file patched by the same mods should hash the same every time. When it does not, a patch produces different
        // XML on every load, and every compiled prefab built on it is rebuilt in every session. Worth knowing which one.
        if (previous is not null)
            Trace.TraceInformation("UIExtenderEx: prefab '{0}' was patched differently than before in this session ({1} -> {2})", prefabName, previous.Substring(0, 8), hash.Substring(0, 8));
    }

    /// <summary>Attempts to retrieve the recorded XML hash for the specified prefab name.</summary>
    public static bool TryGetHash(string prefabName, [MaybeNullWhen(false)] out string hash) => Hashes.TryGetValue(prefabName, out hash);

    /// <summary>Associates the parsed <see cref="WidgetPrefab"/> instance and its prefab name with the SHA-256 hash of its XML document.</summary>
    public static void Record(WidgetPrefab prefab, string prefabName, XmlDocument document)
    {
        var hash = Hash(document.OuterXml);
        ParsedHashes.Add(prefab, new(hash));
        Record(prefabName, hash);
    }

    /// <summary>Attempts to retrieve the recorded XML hash associated with the specified <see cref="WidgetPrefab"/> instance.</summary>
    public static bool TryGetHash(WidgetPrefab prefab, [MaybeNullWhen(false)] out string hash)
    {
        if (ParsedHashes.TryGetValue(prefab, out var recorded))
        {
            hash = recorded.Hash;
            return true;
        }
        hash = null;
        return false;
    }

    /// <summary>Clears all recorded hashes and increments the registry version counter. Intended for tests.</summary>
    public static void Clear()
    {
        Hashes.Clear();
        ParsedHashes = new();
        Touch();
    }

    /// <summary>Computes a hexadecimal SHA-256 hash string for the provided text.</summary>
    public static string Hash(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}