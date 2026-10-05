using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.ResourceManager;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies single-parse snapshot consistency across prefab fingerprint hashing and code generation.
/// <para>
/// Because compilation inspects a movie's prefab hierarchy twice (first to compute its fingerprint hash and second
/// to generate C# source), <see cref="PrefabCompilationSnapshot"/> pins parsed prefab instances in memory.
/// This prevents document drift between passes and releases pinned prefabs once compilation concludes.
/// </para>
/// </summary>
public class PrefabCompilationSnapshotTests
{
    private const string Movie = "SnapshotMovie";
    private const string StatefulPrefabName = "SnapshotStatefulPrefab";
    private const string MoviePrefab = $"<Prefab><Window><Widget><Children><{StatefulPrefabName} /></Children></Widget></Window></Prefab>";

    private PrefabWorkspace? _workspace;
    private UIExtender? _extender;

    /// <summary>
    /// Tracks the total number of times the test factory constructed a new XML document.
    /// </summary>
    private static int _parses;

    private static string? _lastId;

    [SetUp]
    public void SetUp()
    {
        _extender = UIExtender.Create("TestModule.CompiledPrefabs.Snapshot");
        _workspace = new PrefabWorkspace((Movie, MoviePrefab));
        _parses = 0;
        _lastId = null;

        // Registers a dynamic prefab factory that generates fresh XML on each invocation to verify parse stability.
        var workspace = _workspace;
        if (!WidgetFactoryManager.IsRegisteredCustomType(StatefulPrefabName))
        {
            WidgetFactoryManager.Register(StatefulPrefabName, () =>
            {
                _parses++;
                _lastId = "parse" + _parses;
                var document = new XmlDocument();
                document.LoadXml($"<Prefab><Window><Widget Id=\"{_lastId}\" /></Window></Prefab>");
                PrefabXmlRegistry.Record(StatefulPrefabName, document);
                return WidgetPrefabPatch.LoadFromDocument(workspace!.WidgetFactory.PrefabExtensionContext, workspace.WidgetFactory.WidgetAttributeContext, StatefulPrefabName + ".xml", document);
            });
        }
        PrefabFingerprint.ClearCache();
    }

    [TearDown]
    public void TearDown()
    {
        _extender?.Deregister();
        _workspace?.Dispose();
        PrefabFingerprint.ClearCache();
    }

    /// <summary>
    /// Verifies that fingerprint hashing and source generation operate on the identical parsed prefab document from a single parse.
    /// </summary>
    [Test]
    public void OneCompilation_HashesAndGeneratesFromTheSameParse()
    {
        using var snapshot = PrefabCompilationSnapshot.Begin(_workspace!.WidgetFactory, Movie, typeof(CodegenTestVM))!;
        Assert.That(snapshot, Is.Not.Null);
        var parsesAfterSnapshot = _parses;
        var idAtSnapshot = _lastId;

        var code = string.Join("\n", GenerateFrom(snapshot).Select(x => x.Content));

        Assert.That(parsesAfterSnapshot, Is.EqualTo(1), "the snapshot parses the tree once");
        Assert.That(_parses, Is.EqualTo(1), "and the generator is served that parse, not a fresh one");
        Assert.That(code, Does.Contain($"Id = \"{idAtSnapshot}\""), "the code was generated from the document the hash was taken from");
    }

    /// <summary>
    /// Verifies that fingerprinting and generation execute separate parses when operating without an active snapshot.
    /// </summary>
    [Test]
    public void WithoutASnapshot_TheHashAndTheGeneratorSeeDifferentParses()
    {
        // Verifies the baseline behavior without a snapshot where separate invocations result in distinct document parses.
        TestFingerprint.Compute(_workspace!.WidgetFactory, Movie, typeof(CodegenTestVM));
        var afterFingerprint = _parses;
        PrefabFingerprint.ClearCache();
        _workspace.Generate(Movie, typeof(CodegenTestVM));

        Assert.That(afterFingerprint, Is.EqualTo(1));
        Assert.That(_parses, Is.EqualTo(2));
    }

    /// <summary>
    /// Verifies that a disposed snapshot releases all acquired prefab references from the workspace.
    /// </summary>
    [Test]
    public void ASnapshot_ReleasesEveryPrefabItHeld()
    {
        using (var snapshot = PrefabCompilationSnapshot.Begin(_workspace!.WidgetFactory, Movie, typeof(CodegenTestVM))!)
        {
            Assert.That(snapshot.PrefabNames, Does.Contain(Movie));
            Assert.That(_workspace.LivePrefabNames, Does.Contain(Movie), "held while the compilation is prepared");
            GenerateFrom(snapshot);
        }

        Assert.That(_workspace!.LivePrefabNames, Is.Empty, "and released afterwards, so a patch enabled later still reaches the prefab");
        Assert.That(WidgetFactoryManager.IsRegisteredCustomType(StatefulPrefabName), Is.True);
    }

    /// <summary>
    /// Verifies that snapshot cleanup releases all held prefabs even if code generation throws an exception.
    /// </summary>
    [Test]
    public void ASnapshot_ReleasesEveryPrefab_WhenGenerationFails()
    {
        using (var snapshot = PrefabCompilationSnapshot.Begin(_workspace!.WidgetFactory, Movie, typeof(CodegenTestVM))!)
        {
            // Simulates a generation failure to verify that acquired prefab leases are reliably released upon exception.
            Assert.That(() => GenerateFrom(snapshot, movieName: "SnapshotNoSuchMovie"), Throws.InstanceOf<Exception>());
        }

        Assert.That(_workspace!.LivePrefabNames, Is.Empty);
    }

    /// <summary>
    /// Verifies that a snapshot is not created when a dependent prefab lacks a recorded XML hash.
    /// </summary>
    [Test]
    public void ASnapshot_IsNotOpenedWhenAPrefabHasNoRecordedHash()
    {
        var factory = _workspace!.WidgetFactory;
        // Retains the parsed prefab in the factory cache so clearing the registry cannot be repaired by a reparse.
        factory.GetCustomType(Movie);
        PrefabXmlRegistry.Clear();
        PrefabFingerprint.ClearCache();

        Assert.That(PrefabCompilationSnapshot.Begin(factory, Movie, typeof(CodegenTestVM)), Is.Null);

        factory.OnUnload(Movie);
        Assert.That(WidgetFactoryLookup.PrefabLease.Current, Is.Null, "a snapshot that never opened leaves nothing behind");
    }

    /// <summary>
    /// Verifies that compilation inputs accurately reflect pinned prefab hashes and supply the required assembly reference closure.
    /// </summary>
    [Test]
    public void TheInputs_CoverThePinnedParse_AndHandTheJobItsReferences()
    {
        using var snapshot = PrefabCompilationSnapshot.Begin(_workspace!.WidgetFactory, Movie, typeof(CodegenTestVM))!;

        var inputs = snapshot.CollectInputs();
        // Composes the fingerprint key text using the snapshot's pinned section hashes.
        var key = PrefabFingerprint.Compose(snapshot.WidgetFactory, typeof(CodegenTestVM), snapshot.Section, out var keyText);

        Assert.That(inputs.ReferencePaths, Is.Not.Empty);
        Assert.That(key, Is.EqualTo(inputs.Fingerprint));
        Assert.That(keyText, Does.Not.Contain("assembly:"));
        Assert.That(keyText, Does.Contain("prefab:" + StatefulPrefabName + ":" + snapshot.Section.Hashes[StatefulPrefabName]), "the hash of the parse the generator is served");
        Assert.That(snapshot.CollectInputs().Fingerprint, Is.EqualTo(inputs.Fingerprint), "nothing changed in between, so collecting again agrees");
    }

    private IReadOnlyList<GeneratedSource> GenerateFrom(IPrefabCompilationSnapshot snapshot, string? movieName = null)
    {
        var typed = (PrefabCompilationSnapshot) snapshot;
        var context = new PrefabCodeGenerator("Bannerlord.UIExtenderEx.Tests.Generated", typed.WidgetFactory, null!, null!);
        context.AddMovie(movieName ?? typed.MovieName, typed.ViewModelType.FullName, typed.ViewModelType);
        return [.. context.GenerateInMemory().Select(x => new GeneratedSource(x.Key, x.Value))];
    }
}
