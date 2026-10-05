using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;

using NUnit.Framework;

using System;
using System.IO;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies metadata cache invalidation in <see cref="RoslynCompiler"/> when referenced assembly binaries are replaced on disk.
/// <para>
/// The Roslyn compiler caches imported metadata references across compilations to minimize warmup overhead.
/// When external tools replace a library assembly while the game runs, the cache must invalidate stale metadata
/// based on timestamps or MVIDs, ensuring downstream compilations bind against the newly deployed binary.
/// </para>
/// </summary>
public class RoslynReferenceCacheTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "UIExtenderEx-references-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_directory, true); }
        catch (IOException) { }
    }

    private static readonly string Core = typeof(object).Assembly.Location;

    private string BuildLibrary(RoslynCompiler compiler, string source)
    {
        var result = compiler.Compile("Probe.Library", [new GeneratedSource("Probe.cs", source)], [Core]);
        Assert.That(result.Errors, Is.Empty, "test premise: the library compiles");
        var path = Path.Combine(_directory, "Probe.Library.dll");
        File.WriteAllBytes(path, result.Assembly!);
        return path;
    }

    [Test]
    public void ALibraryReplacedOnDisk_IsCompiledAgainstItsNewBuild()
    {
        var compiler = new RoslynCompiler();
        var library = BuildLibrary(compiler, "public class Probe { }");
        var first = compiler.Compile("Probe.Consumer", [new GeneratedSource("Consumer.cs", "class C { Probe P; }")], [Core, library]);
        Assert.That(first.Errors, Is.Empty, "test premise: the old build is referenced, and kept");

        // Simulates an external mod manager updating a binary on disk during active execution.
        var replaced = BuildLibrary(compiler, "public class Probe { public int Added; }");
        File.SetLastWriteTimeUtc(replaced, File.GetLastWriteTimeUtc(replaced).AddSeconds(2));

        var second = compiler.Compile("Probe.Consumer", [new GeneratedSource("Consumer.cs", "class C { int M() => new Probe().Added; }")], [Core, library]);

        Assert.That(second.Errors, Is.Empty, "the member only the new build has must bind");
    }
}
