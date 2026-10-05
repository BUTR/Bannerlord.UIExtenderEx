using Bannerlord.UIExtenderEx.CompiledPrefabs;

using NUnit.Framework;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies storage, concurrent write handling, persistence, and invalidation semantics in <see cref="PrefabCache"/>.
/// <para>
/// Higher-level orchestration across the manager lifecycle is verified in <see cref="CompiledPrefabManagerTests"/>.
/// </para>
/// </summary>
public class PrefabCacheTests
{
    private const string Generation = "generation-1";

    private string _directory = string.Empty;

    private string FilePath => Path.Combine(_directory, PrefabCache.FileName);

    [SetUp]
    public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "UIExtenderEx-prefab-cache-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_directory, true); } catch (Exception) { /* ignore */ }
    }

    /// <summary>
    /// Verifies that cached assemblies and fingerprints persist across cache reopen operations.
    /// </summary>
    [Test]
    public void ABuild_SurvivesAReopen()
    {
        var cache = new PrefabCache(_directory, Generation, []);
        Put(cache, "Movie", "fingerprint-a", [1, 2, 3]);
        cache.Flush();

        var reopened = new PrefabCache(_directory, Generation, []);

        Assert.That(Read(reopened, "Movie", "fingerprint-a", "Variant"), Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(reopened.FindBuildOf("Movie", "Variant")!.Fingerprint, Is.EqualTo("fingerprint-a"));
        Assert.That(Directory.EnumerateFiles(_directory).Select(Path.GetFileName), Is.EquivalentTo(new[] { PrefabCache.FileName }), "no temporary file is left behind");
    }

    /// <summary>
    /// Verifies that cached dependency identifiers persist in ordered sequence across cache reopen operations.
    /// </summary>
    [Test]
    public void Dependencies_SurviveAReopen_InOrder()
    {
        var cache = new PrefabCache(_directory, Generation, []);
        cache.Put("Movie", "Variant", "fingerprint-a", [1], ["A.Mod:0123", "System.Xml:v4.0.0.0", "TaleWorlds.Library:abcd"]);
        cache.Flush();

        var reopened = new PrefabCache(_directory, Generation, []);

        Assert.That(reopened.FindBuildOf("Movie", "Variant")!.Dependencies, Is.EqualTo(new[] { "A.Mod:0123", "System.Xml:v4.0.0.0", "TaleWorlds.Library:abcd" }));
    }

    /// <summary>
    /// Verifies that pending cache additions are readable from memory before being written to disk.
    /// </summary>
    [Test]
    public void APutBuild_IsReadableBeforeItIsWritten()
    {
        var cache = new PrefabCache(_directory, Generation, []);
        Put(cache, "Movie", "fingerprint-a", [1, 2, 3]);

        Assert.That(Read(cache, "Movie", "fingerprint-a"), Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(PrefabCacheArchive.Read(FilePath).Entries, Is.Empty);
    }

    /// <summary>
    /// Verifies that concurrent writes from multiple worker threads flush reliably without data loss.
    /// </summary>
    [Test]
    public void BuildsPutFromSeveralWorkers_AreAllWritten()
    {
        var cache = new PrefabCache(_directory, Generation, []);

        Parallel.For(0, 16, i =>
        {
            Put(cache, "Movie" + i, "fingerprint-" + i, [(byte) i]);
            cache.Flush();
        });

        var onDisk = PrefabCacheArchive.Read(FilePath).Entries;
        Assert.That(onDisk.Select(x => x.Movie), Is.EquivalentTo(Enumerable.Range(0, 16).Select(i => "Movie" + i)));
        var reopened = new PrefabCache(_directory, Generation, []);
        Assert.That(Enumerable.Range(0, 16).All(i => Read(reopened, "Movie" + i, "fingerprint-" + i, "Variant")?.SequenceEqual(new[] { (byte) i }) == true), Is.True);
    }

    /// <summary>
    /// Verifies that failed disk writes due to external file locks preserve pending in-memory cache entries for subsequent flush attempts.
    /// </summary>
    [Test]
    public void AWriteThatCannotHappen_IsKeptForTheNextFlush()
    {
        var cache = new PrefabCache(_directory, Generation, []);
        Put(cache, "Earlier", "fingerprint-earlier", [7]);
        cache.Flush();

        Put(cache, "Movie", "fingerprint-a", [1, 2, 3]);
        using (new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            cache.Flush();

        Assert.That(Read(cache, "Movie", "fingerprint-a", "Variant"), Is.EqualTo(new byte[] { 1, 2, 3 }), "still served from memory");

        cache.Flush();
        Assert.That(PrefabCacheArchive.Read(FilePath).Entries.Select(x => x.Movie), Is.EquivalentTo(new[] { "Earlier", "Movie" }));
    }

    /// <summary>
    /// Verifies that cache archives with mismatched generation identifiers are discarded and reinitialized.
    /// </summary>
    [Test]
    public void ArchiveOfAnotherGeneration_IsStartedOverOnOpen()
    {
        var cache = new PrefabCache(_directory, "generation-0", []);
        Put(cache, "Movie", "fingerprint-a", [1]);
        cache.Flush();

        var reopened = new PrefabCache(_directory, Generation, []);

        Assert.That(reopened.Builds, Is.Empty);
        var onDisk = PrefabCacheArchive.Read(FilePath);
        Assert.That(onDisk.Generation, Is.EqualTo(Generation));
        Assert.That(onDisk.Entries, Is.Empty);
    }

    /// <summary>
    /// Verifies that seed archives provide fallback read entries without being modified by runtime writes.
    /// </summary>
    [Test]
    public void ASeed_IsNeverWritten()
    {
        var seedDirectory = Path.Combine(_directory, "seed");
        var seed = new PrefabCache(seedDirectory, Generation, []);
        Put(seed, "Movie", "fingerprint-a", [1]);
        seed.Flush();
        var seedFile = Path.Combine(seedDirectory, PrefabCache.FileName);
        var before = File.ReadAllBytes(seedFile);

        var cache = new PrefabCache(_directory, Generation, [seedFile]);
        Put(cache, "Movie", "fingerprint-b", [2]);
        cache.Flush();

        Assert.That(Read(cache, "Movie", "fingerprint-a", "Variant"), Is.EqualTo(new byte[] { 1 }));
        Assert.That(File.ReadAllBytes(seedFile), Is.EqualTo(before));
    }

    /// <summary>
    /// Verifies that seed archives belonging to incompatible generations are ignored.
    /// </summary>
    [Test]
    public void ASeedOfAnotherGeneration_IsIgnored()
    {
        var seedDirectory = Path.Combine(_directory, "seed");
        var seed = new PrefabCache(seedDirectory, "generation-0", []);
        Put(seed, "Movie", "fingerprint-a", [1]);
        seed.Flush();

        var cache = new PrefabCache(_directory, Generation, [Path.Combine(seedDirectory, PrefabCache.FileName)]);

        Assert.That(Read(cache, "Movie", "fingerprint-a", "Variant"), Is.Null);
    }

    /// <summary>
    /// Verifies that ViewModel names containing characters invalid for file systems are safely sanitized and stored as distinct entries.
    /// </summary>
    [Test]
    public void ViewModelNamesThatAreNoFileNames_AreStillStored()
    {
        var cache = new PrefabCache(_directory, Generation, []);
        cache.Put("Movie", "Some.Generic`1[[A, B, Version=1.0.0.0]]", "fingerprint-a", [1]);
        cache.Put("Movie", "Some.Other:Name", "fingerprint-b", [2]);
        cache.Flush();

        var reopened = new PrefabCache(_directory, Generation, []);
        Assert.That(Read(reopened, "Movie", "fingerprint-a", "Some.Generic`1[[A, B, Version=1.0.0.0]]"), Is.EqualTo(new byte[] { 1 }));
        Assert.That(Read(reopened, "Movie", "fingerprint-b", "Some.Other:Name"), Is.EqualTo(new byte[] { 2 }));
    }

    private static byte[]? Read(PrefabCache cache, string movie, string fingerprint, string variant = "Variant") =>
        cache.TryRead(movie, variant, fingerprint, _ => true)?.Assembly;

    private static void Put(PrefabCache cache, string movie, string fingerprint, byte[] assembly) =>
        cache.Put(movie, "Variant", fingerprint, assembly);
}
