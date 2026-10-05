using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.ResourceManager;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Measures the runtime performance overhead of fingerprint validation and cache lookup prior to movie instantiation.
/// <para>
/// Evaluates cached dictionary lookups versus full hierarchy traversals and re-parsing passes, measuring performance
/// impacts when runtime-registered custom prefabs force section rebuilds.
/// </para>
/// <para>
/// Execute via <c>dotnet test --filter FullyQualifiedName~PrefabLoadBenchmarks</c> to inspect benchmark metrics.
/// </para>
/// </summary>
[Explicit("Timing measurements, not a correctness check.")]
public class PrefabLoadBenchmarks
{
    private const int Warmup = 20;
    private const int Iterations = 200;

    private UIExtender _extender = null!;

    [SetUp]
    public void SetUp()
    {
        // Applies runtime patches, including XML hash recording during widget prefab loading.
        _extender = UIExtender.Create("TestModule.CompiledPrefabs.LoadBenchmark");
        _extender.Register([]);
    }

    [TearDown]
    public void TearDown()
    {
        _extender.Deregister();
        PrefabFingerprint.ClearCache();
        PrefabReferenceSet.ClearCache();
    }

    /// <summary>
    /// Constructs a simulated prefab tree of specified hierarchy depth to benchmark screen structures.
    /// </summary>
    private static (string Name, string Xml)[] Tree(string prefix, int depth, string? extraChild = null)
    {
        var prefabs = new List<(string, string)>();
        for (var i = 0; i < depth; i++)
        {
            var child = i + 1 < depth ? $"<{prefix}Nested{i + 1} />" : string.Empty;
            var extra = i == 0 && extraChild is not null ? extraChild : string.Empty;
            prefabs.Add(($"{prefix}Nested{i}", $"<Prefab><Window><Widget><Children>{child}{extra}</Children></Widget></Window></Prefab>"));
        }
        prefabs.Insert(0, ($"{prefix}Movie", $"<Prefab><Window><Widget><Children><{prefix}Nested0 /></Children></Widget></Window></Prefab>"));
        return [.. prefabs];
    }

    /// <summary>
    /// Measures the per-open validation duration for file-backed prefab trees across varying hierarchy depths.
    /// </summary>
    [Test]
    public void DecidingToUseACompiledMovie()
    {
        var report = new StringBuilder();
        foreach (var depth in new[] { 1, 8, 24 })
        {
            var prefix = "LoadBench" + depth;
            using var workspace = new PrefabWorkspace(Tree(prefix, depth));
            var factory = workspace.WidgetFactory;
            var movie = prefix + "Movie";

            var cold = Stopwatch.StartNew();
            var fingerprint = PrefabFingerprint.Compute(factory, movie, typeof(CodegenTestVM), out _, out var failure);
            cold.Stop();
            Assert.That(fingerprint, Is.Not.Null, failure);

            Measure(report, $"{depth + 1} prefabs, every open", () => _ = TestFingerprint.Compute(factory, movie, typeof(CodegenTestVM)));
            report.AppendLine($"    (first ever computation of the same movie: {cold.Elapsed.TotalMilliseconds:F1} ms)");
        }
        Report(report);
    }

    /// <summary>
    /// Measures the per-open validation overhead when the prefab hierarchy contains a runtime-registered custom prefab.
    /// <para>
    /// Evaluates the throughput impact when <see cref="PrefabSection.IsCurrent(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory)"/>
    /// returns <see langword="false"/> due to runtime callbacks, triggering hierarchy walks and reference set re-evaluations.
    /// </para>
    /// </summary>
    [Test]
    public void DecidingToUseACompiledMovie_WithARuntimeRegisteredPrefabInItsTree()
    {
        var report = new StringBuilder();
        foreach (var depth in new[] { 1, 8, 24 })
        {
            var prefix = "LoadBenchDyn" + depth;
            var registered = prefix + "Dynamic";
            using var workspace = new PrefabWorkspace(Tree(prefix, depth, $"<{registered} />"));
            var factory = workspace.WidgetFactory;
            var movie = prefix + "Movie";
            if (!WidgetFactoryManager.IsRegisteredCustomType(registered))
            {
                WidgetFactoryManager.Register(registered, () =>
                {
                    var document = new XmlDocument();
                    document.LoadXml(PrefabWorkspace.PlainPrefab);
                    PrefabXmlRegistry.Record(registered, document);
                    return WidgetPrefabPatch.LoadFromDocument(factory.PrefabExtensionContext, factory.WidgetAttributeContext, registered + ".xml", document);
                });
            }
            factory.GetCustomType(registered);

            var fingerprint = PrefabFingerprint.Compute(factory, movie, typeof(CodegenTestVM), out _, out var failure);
            Assert.That(fingerprint, Is.Not.Null, failure);
            Assert.That(PrefabFingerprint.GetPrefabSection(factory, movie)!.IsCurrent(factory), Is.False, "test premise: this movie is on the rebuild path");

            Measure(report, $"{depth + 2} prefabs, one registered, every open",
                () => _ = TestFingerprint.Compute(factory, movie, typeof(CodegenTestVM)));
        }
        Report(report);
    }

    /// <summary>
    /// Benchmarks individual stages of section rebuilds to profile closure traversal and assembly reference collection costs.
    /// </summary>
    [Test]
    public void TheStepsOfARebuiltSection()
    {
        var prefix = "LoadBenchSteps";
        using var workspace = new PrefabWorkspace(Tree(prefix, 24));
        var factory = workspace.WidgetFactory;
        var movie = prefix + "Movie";
        var report = new StringBuilder();

        TestFingerprint.Compute(factory, movie, typeof(CodegenTestVM));
        var section = PrefabFingerprint.GetPrefabSection(factory, movie)!;

        Measure(report, "confirming a cached section is current", () => _ = section.IsCurrent(factory));
        Measure(report, "walking and re-parsing the closure", () => _ = PrefabFingerprint.BuildPrefabSection(factory, movie, out _));
        Measure(report, "collecting the assembly set, cached closure", () => _ = PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), section));
        Measure(report, "collecting the assembly set, new closure",
            () => _ = PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), PrefabFingerprint.BuildPrefabSection(factory, movie, out _)));

        report.AppendLine($"    assemblies in the set: {PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), section).Count}");
        Report(report);
    }

    private static void Measure(StringBuilder report, string name, Action action)
    {
        for (var i = 0; i < Warmup; i++)
            action();

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
            action();
        stopwatch.Stop();

        report.AppendLine($"  {name,-48} {stopwatch.Elapsed.TotalMilliseconds / Iterations,9:F3} ms per open");
    }

    private static void Report(StringBuilder report) => TestContext.Progress.WriteLine(Environment.NewLine + report);
}
