using Bannerlord.UIExtenderEx.Tests.Oracle;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.Oracle.Fuzzing;

/// <summary>
/// Executes fuzz testing over randomly generated prefabs, verifying compiled output against XML loader oracles
/// and automatically shrinking defect reproductions to minimal test cases.
/// <para>
/// Generates synthetic prefab structures (<see cref="PrefabGenerator"/>) combining disparate layout, parameter, and
/// binding constructs to uncover edge cases beyond the static game corpus. Runs against mockable core widgets without
/// requiring a TaleWorlds game installation.
/// </para>
/// <para>
/// Controls reproducible seed ranges via the <c>UIEXTENDEREX_FUZZ_SEED</c> and <c>UIEXTENDEREX_FUZZ_CASES</c> environment variables.
/// </para>
/// </summary>
[NonParallelizable]
[Category("Fuzz")]
[Explicit("Loads an assembly per generated prefab that .NET Framework never unloads; run it on purpose, with UIEXTENDEREX_ORACLE_MAX_MEMORY_MB set")]
public class PrefabFuzzTests
{
    private const int DefaultSeed = 20260929;
    private const int DefaultCases = 60;
    private const int ShrinkBudget = 250;

    [TestCase(false, false, TestName = "GeneratedPrefabs_BuildTheSameTreeCompiledAsFromXml")]
    [TestCase(true, false, TestName = "GeneratedPrefabs_BindTheSameCompiledAsFromXml")]
    [TestCase(true, true, TestName = "GeneratedPrefabs_BindTheSameCompiledAsFromXml_OverObjectTypedViewModels")]
    public void GeneratedPrefabs_BehaveTheSameCompiledAsFromXml(bool bindings, bool objectTyped)
    {
        var kind = !bindings ? "static" : objectTyped ? "bindings-object" : "bindings";
        var seed = int.TryParse(Environment.GetEnvironmentVariable("UIEXTENDEREX_FUZZ_SEED"), out var s) ? s : DefaultSeed;
        var count = int.TryParse(Environment.GetEnvironmentVariable("UIEXTENDEREX_FUZZ_CASES"), out var c) ? c : DefaultCases;

        var stopwatch = Stopwatch.StartNew();
        var cases = Enumerable.Range(seed, count).Select(x => new PrefabGenerator(x, bindings).Next($"Fuzz{x}")).ToList();
        var failures = new List<(FuzzCase Case, OracleResult Result)>();
        var outcomes = new List<OracleResult>();
        const int batch = 30;
        for (var start = 0; start < cases.Count; start += batch)
        {
            var group = cases.Skip(start).Take(batch).ToList();
            // Writes pending test cases to disk prior to execution to diagnose potential unrecoverable stack overflows.
            var pending = Path.Combine(Path.GetTempPath(), "UIExtenderEx-oracle", $"fuzz-{kind}-running.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(pending)!);
            File.WriteAllText(pending, string.Join(Environment.NewLine + Environment.NewLine, group.Select(x => $"Seed {x.Seed}:{Environment.NewLine}{x}")));
            var evaluated = Evaluate(group, bindings, objectTyped);
            outcomes.AddRange(evaluated);
            foreach (var defect in evaluated.Where(x => x.IsDefect))
                failures.Add((group.First(x => x.Movie.Name == defect.Prefab), defect));
        }
        var tally = string.Join(", ", outcomes.GroupBy(x => x.Outcome).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"));
        TestContext.Out.WriteLine($"{count} cases from seed {seed} in {stopwatch.Elapsed.TotalSeconds:F1} s, {failures.Count} failing ({tally})");
        // Logs ungenerated test cases that defaulted to XML fallback to report coverage metrics.
        foreach (var sample in outcomes.Where(x => x.Outcome is not OracleOutcome.Match).GroupBy(x => x.Outcome).Select(x => x.First()))
            TestContext.Out.WriteLine($"  {sample.Outcome}, e.g. {sample.Prefab}: {sample.Detail?.Split('\n')[0].Trim()}");

        // Asserts that the test harness successfully prepared all generated cases before shrinking failures.
        var untested = outcomes.Where(x => x.Outcome == OracleOutcome.HarnessFailed).ToList();
        Assert.That(untested, Is.Empty, () => $"{untested.Count} generated prefabs could not be tested: {string.Join(" | ", untested.Take(3).Select(x => $"{x.Prefab}: {x.Detail}"))}");
        if (failures.Count == 0)
            return;

        var (failing, first) = failures[0];
        var (minimal, result) = Shrink(failing, first, bindings, objectTyped);
        var report = Path.Combine(Path.GetTempPath(), "UIExtenderEx-oracle", $"fuzz-{kind}-{failing.Seed}.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, $"Seed {failing.Seed}, {failures.Count} failing cases: {string.Join(", ", failures.Select(x => x.Case.Seed))}{Environment.NewLine}" +
                                  $"{result.Outcome}: {result.Detail}{Environment.NewLine}{string.Join(Environment.NewLine, result.Differences.Take(30))}{Environment.NewLine}{Environment.NewLine}" +
                                  $"Shrunk to:{Environment.NewLine}{minimal}{Environment.NewLine}{Environment.NewLine}As generated:{Environment.NewLine}{failing}");
        Assert.Fail($"{failures.Count} of {count} generated prefabs behave differently compiled than as XML (seeds {string.Join(", ", failures.Take(10).Select(x => x.Case.Seed))}). " +
                    $"The first, shrunk from size {failing.Size} to {minimal.Size}:{Environment.NewLine}{result.Outcome}: {result.Detail}{Environment.NewLine}" +
                    $"{string.Join(Environment.NewLine, result.Differences.Take(8))}{Environment.NewLine}{minimal}{Environment.NewLine}Report: {report}");
    }

    /// <summary>
    /// Evaluates generated test cases across the appropriate oracle within a unified prefab workspace.
    /// </summary>
    private static List<OracleResult> Evaluate(IReadOnlyList<FuzzCase> cases, bool bindings, bool objectTyped)
    {
        using var workspace = new PrefabWorkspace([.. cases.SelectMany(x => x.Prefabs).Select(x => (x.Name, x.ToXml()))]);
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(PrefabFuzzTests));
        var widgetAssemblies = new[] { typeof(LabelWidget).Assembly };
        var movies = cases.Select(x => x.Movie.Name).ToList();
        if (bindings)
            return new BindingLoaderOracle(workspace.WidgetFactory, ui.SpriteData, ui.BrushFactory, ui.Context, widgetAssemblies, objectTyped).Run(movies);

        var plainFactory = new WidgetFactory(workspace.ResourceDepot, "Prefabs");
        plainFactory.Initialize();
        return new LoaderOracle(plainFactory, ui.SpriteData, ui.BrushFactory, ui.Context, widgetAssemblies).Run(movies);
    }

    /// <summary>
    /// Minimizes a failing test case greedily by iteratively adopting smaller candidates that continue to exhibit defects.
    /// </summary>
    private static (FuzzCase Case, OracleResult Result) Shrink(FuzzCase failing, OracleResult result, bool bindings, bool objectTyped)
    {
        var current = failing;
        var evaluations = 0;
        var progress = true;
        while (progress && evaluations < ShrinkBudget)
        {
            progress = false;
            foreach (var candidate in current.Shrinks())
            {
                if (++evaluations > ShrinkBudget)
                    break;
                OracleMemoryGuard.Check($"shrinking seed {failing.Seed}, evaluation {evaluations}");
                if (Evaluate([candidate], bindings, objectTyped).FirstOrDefault(x => x.IsDefect) is { } stillFailing)
                {
                    current = candidate;
                    result = stillFailing;
                    progress = true;
                    break;
                }
            }
        }
        return (current, result);
    }
}
