using Bannerlord.UIExtenderEx.Tests.Oracle;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Bannerlord.UIExtenderEx.Corpus.Tests;

/// <summary>
/// Verifies structural and binding parity across every prefab shipped by the game, comparing XML loader behavior
/// against compiled C# implementations using <see cref="LoaderOracle"/> and <see cref="BindingLoaderOracle"/>.
/// <para>
/// Executes when a valid game installation is detected via <c>BANNERLORD_GAME_DIR</c> or standard Steam directories.
/// Generates comprehensive execution reports logging outcomes and discrepancies for every evaluated prefab.
/// </para>
/// </summary>
[NonParallelizable]
[Category("Corpus")]
public class GamePrefabCorpusTests
{
    [Test]
    public void EveryOfficialPrefab_BuildsTheSameTreeCompiledAsFromXml() =>
        RunCorpus("corpus", GameInstallation.OfficialModules, game => game.WidgetFactory.GetPrefabNames(), "static");

    /// <summary>
    /// Evaluates official game prefabs as data-bound movies against synthesized view models, driving properties,
    /// child view models, lists, and commands across both XML and compiled pipelines (<see cref="BindingLoaderOracle"/>).
    /// </summary>
    [Test]
    [Explicit(HeavyRun)]
    public void EveryOfficialPrefab_BindsTheSameCompiledAsFromXml() =>
        RunCorpus("bindings", GameInstallation.OfficialModules, game => game.BindingWidgetFactory.GetPrefabNames(), "bindings");

    /// <summary>
    /// Evaluates official game prefabs against synthesized view models with weakly typed (<see cref="object"/>) properties,
    /// exercising late-bound name-based resolution side-by-side with strongly typed access paths.
    /// </summary>
    [Test]
    [Explicit(HeavyRun)]
    public void EveryOfficialPrefab_BindsTheSameCompiledAsFromXml_OverObjectTypedViewModels() =>
        RunCorpus("bindings-object", GameInstallation.OfficialModules, game => game.BindingWidgetFactory.GetPrefabNames(), "bindings-object");

    /// <summary>
    /// Evaluates prefabs provided by installed third-party modules to test custom widget constructs beyond official prefabs.
    /// </summary>
    [Test]
    [Explicit(HeavyRun)]
    public void EveryModPrefab_BuildsTheSameTreeCompiledAsFromXml() =>
        RunModCorpus("mods-corpus", "static");

    [Test]
    [Explicit(HeavyRun)]
    public void EveryModPrefab_BindsTheSameCompiledAsFromXml() =>
        RunModCorpus("mods-bindings", "bindings");

    /// <summary>
    /// Evaluates runtime-dumped prefabs including applied mod patches captured during live game sessions.
    /// Requires setting <c>UIEXTENDEREX_DUMP_PREFABS=&lt;folder&gt;</c> during gameplay and supplying that directory
    /// via <c>UIEXTENDEREX_ORACLE_PREFAB_DUMP</c>.
    /// </summary>
    [Test]
    [Explicit(HeavyRun)]
    public void EveryDumpedPrefab_BuildsTheSameTreeCompiledAsFromXml() =>
        RunDumpCorpus("dump-corpus", "static");

    [Test]
    [Explicit(HeavyRun)]
    public void EveryDumpedPrefab_BindsTheSameCompiledAsFromXml() =>
        RunDumpCorpus("dump-bindings", "bindings");

    private static void RunDumpCorpus(string name, string kind)
    {
        if (GameInstallation.DumpDirectory is not { } dump)
        {
            Assert.Ignore("Set UIEXTENDEREX_ORACLE_PREFAB_DUMP to a folder a game session dumped its prefabs into (UIEXTENDEREX_DUMP_PREFABS).");
            return;
        }
        var (dumped, modules) = GameInstallation.ReadDump(dump);
        if (dumped.Count == 0)
        {
            Assert.Ignore($"'{dump}' holds no prefab.");
            return;
        }
        // The official ones first, as the game loads them, then the session's others in its order
        RunCorpus(name, [.. GameInstallation.OfficialModules, .. modules.Where(x => !GameInstallation.OfficialModules.Contains(x) && !x.StartsWith("Bannerlord.UIExtenderEx", StringComparison.OrdinalIgnoreCase))],
            game =>
            {
                var factory = kind == "static" ? game.WidgetFactory : game.BindingWidgetFactory;
                // Nothing is tested unless the dumped file is the one the factory reads: the prefab as the game parsed it
                var notFromDump = dumped.Where(x => !factory.GetCustomTypePath(x).Replace('\\', '/').StartsWith(dump.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)).ToList();
                Assert.That(notFromDump, Is.Empty, "dumped prefabs the factory reads from elsewhere");
                return dumped;
            }, kind);
    }

    /// <summary>
    /// Explains why corpus runs require explicit test selection: assembly loading overhead accumulates indefinitely in .NET Framework.
    /// </summary>
    private const string HeavyRun = "Holds an assembly per prefab for the life of the process; run on purpose, a subset at a time (UIEXTENDEREX_ORACLE_PREFABS)";

    private static void RunModCorpus(string name, string kind)
    {
        if (GameInstallation.Find() is not { } directory)
        {
            Assert.Ignore("No game installation found; set BANNERLORD_GAME_DIR to run this test.");
            return;
        }
        var mods = GameInstallation.InstalledMods(directory);
        if (mods.Count == 0)
        {
            Assert.Ignore("No installed mod ships prefabs.");
            return;
        }
        RunCorpus(name, [.. GameInstallation.OfficialModules, .. mods],
            game => mods.SelectMany(game.PrefabsOf).Intersect((kind == "static" ? game.WidgetFactory : game.BindingWidgetFactory).GetPrefabNames()), kind);
    }

    /// <param name="kind">The oracle evaluation mode: <c>"static"</c>, <c>"bindings"</c>, or <c>"bindings-object"</c> (<see cref="OracleChunks"/>).</param>
    private static void RunCorpus(string name, IEnumerable<string> modules, Func<GameInstallation, IEnumerable<string>> prefabsOf, string kind)
    {
        if (GameInstallation.Find() is not { } directory)
        {
            Assert.Ignore("No game installation found; set BANNERLORD_GAME_DIR to run this test.");
            return;
        }

        // Discovers candidate prefabs in the parent runner; executes oracle evaluation across child worker chunk processes.
        var stopwatch = Stopwatch.StartNew();
        var moduleList = modules.ToList();
        var game = GameInstallation.Load(directory, moduleList);
        var loaded = stopwatch.Elapsed;
        var prefabs = Select(prefabsOf(game));
        var (results, chunks) = OracleChunks.Run(kind, game.Modules, prefabs);

        var report = WriteReport(name, results, game, loaded, stopwatch.Elapsed, chunks);
        TestContext.Out.WriteLine(Summary(results));
        TestContext.Out.WriteLine($"Full report: {report}");

        // Verifies that all chunk processes completed without unhandled crashes, memory limits, or timeouts.
        var failedChunks = chunks.Where(x => x.Contains(", failed: ")).ToList();
        Assert.That(failedChunks, Is.Empty, $"{failedChunks.Count} chunks failed:{Environment.NewLine}{string.Join(Environment.NewLine, failedChunks)}{Environment.NewLine}Full report: {report}");
        // Verifies that the test harness successfully prepared all evaluated prefabs.
        var untested = results.Where(x => x.Outcome == OracleOutcome.HarnessFailed).ToList();
        Assert.That(untested, Is.Empty, $"{untested.Count} prefabs could not be tested:{Environment.NewLine}{string.Join(Environment.NewLine, untested.Take(20).Select(Headline))}{Environment.NewLine}Full report: {report}");

        var defects = results.Where(x => x.IsDefect).ToList();
        Assert.That(defects, Is.Empty, $"{defects.Count} of {results.Count} prefabs behave differently compiled than as XML. {Summary(results)}" +
            Environment.NewLine + string.Join(Environment.NewLine, defects.Take(40).Select(Headline)) + Environment.NewLine + $"Full report: {report}");
    }

    /// <summary>
    /// Filters prefab candidate names based on the <c>UIEXTENDEREX_ORACLE_PREFABS</c> environment variable filter (comma-separated names or count prefix).
    /// </summary>
    private static List<string> Select(IEnumerable<string> names)
    {
        var all = names.OrderBy(x => x, StringComparer.Ordinal).ToList();
        var filter = Environment.GetEnvironmentVariable("UIEXTENDEREX_ORACLE_PREFABS");
        if (string.IsNullOrWhiteSpace(filter))
            return all;
        if (int.TryParse(filter, out var count))
            return [.. all.Take(count)];
        var wanted = filter!.Split(',').Select(x => x.Trim()).ToHashSet(StringComparer.Ordinal);
        return [.. all.Where(wanted.Contains)];
    }

    private static string Summary(IEnumerable<OracleResult> results) => string.Join(", ", results
        .GroupBy(x => x.Outcome).OrderBy(x => x.Key).Select(x => $"{x.Key} {x.Count()}"));

    private static string Headline(OracleResult result) => result.Outcome switch
    {
        OracleOutcome.Differs => $"{result.Prefab}: {result.Differences.Count} differences, first {result.Differences[0]}",
        _ when result.XmlAsserts != result.CompiledAsserts && result.Outcome == OracleOutcome.Match => $"{result.Prefab}: {result.Detail}",
        _ => $"{result.Prefab}: {result.Outcome} - {FirstLine(result.Detail)}",
    };

    private static string FirstLine(string? text) => text?.Split('\n')[0].Trim() ?? "";

    private static string WriteReport(string name, List<OracleResult> results, GameInstallation game, TimeSpan loaded, TimeSpan total, IEnumerable<string> chunks)
    {
        var directory = Path.Combine(Path.GetTempPath(), "UIExtenderEx-oracle");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        var sb = new StringBuilder();
        sb.AppendLine($"Game: {game.Directory}");
        sb.AppendLine($"Modules: {string.Join(", ", game.Modules)}");
        sb.AppendLine($"Loading the game's GUI: {loaded.TotalSeconds:F1} s, total {total.TotalSeconds:F1} s");
        sb.AppendLine(Summary(results));
        foreach (var chunk in chunks)
            sb.AppendLine(chunk);
        sb.AppendLine();
        foreach (var result in results.OrderBy(x => x.IsDefect ? 0 : 1).ThenBy(x => x.Outcome).ThenBy(x => x.Prefab, StringComparer.Ordinal))
        {
            sb.AppendLine($"== {result.Prefab}: {result.Outcome}{(result.XmlAsserts != result.CompiledAsserts ? $" (asserts XML {result.XmlAsserts}, compiled {result.CompiledAsserts})" : "")}");
            if (result.Detail is not null)
                sb.AppendLine("   " + result.Detail.Replace("\n", "\n   "));
            foreach (var difference in result.Differences.Take(200))
                sb.AppendLine("   " + difference);
            if (result.Differences.Count > 200)
                sb.AppendLine($"   ... {result.Differences.Count - 200} more");
        }
        File.WriteAllText(path, sb.ToString());
        return path;
    }
}