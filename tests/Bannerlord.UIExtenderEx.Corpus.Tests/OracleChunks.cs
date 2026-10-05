using Bannerlord.UIExtenderEx.Tests.Oracle;

using Newtonsoft.Json;

using NUnit.Framework;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Corpus.Tests;

/// <summary>
/// Partitions corpus oracle evaluations across isolated child worker processes to manage working set memory ceilings.
/// <para>
/// Reclaims native and managed resources by terminating child processes upon chunk completion, mitigating .NET Framework
/// assembly unload limitations. Enforces timeout thresholds and distributes memory ceilings across concurrent workers (<c>UIEXTENDEREX_ORACLE_PARALLEL</c>).
/// </para>
/// </summary>
internal static class OracleChunks
{
    private static readonly TimeSpan ChunkTimeLimit = TimeSpan.FromMinutes(15);

    internal const string WorkerVariable = "UIEXTENDEREX_ORACLE_WORKER";
    internal const string KindVariable = "UIEXTENDEREX_ORACLE_WORKER_KIND";
    internal const string ModulesVariable = "UIEXTENDEREX_ORACLE_WORKER_MODULES";
    internal const string PrefabsVariable = "UIEXTENDEREX_ORACLE_WORKER_PREFABS";
    internal const string ResultsVariable = "UIEXTENDEREX_ORACLE_WORKER_RESULTS";

    /// <summary>
    /// Represents serialization data returned by a worker chunk process, containing results, peak memory metrics, and failures.
    /// </summary>
    internal sealed class ChunkReport
    {
        public List<ResultRecord> Results { get; set; } = [];
        public long PeakMegabytes { get; set; }
        public string? Failure { get; set; }
    }

    internal sealed class ResultRecord
    {
        public string Prefab { get; set; } = "";
        public OracleOutcome Outcome { get; set; }
        public List<string> Differences { get; set; } = [];
        public string? Detail { get; set; }
        public int XmlAsserts { get; set; }
        public int CompiledAsserts { get; set; }

        public static ResultRecord From(OracleResult result) => new()
        {
            Prefab = result.Prefab,
            Outcome = result.Outcome,
            Differences = [.. result.Differences],
            Detail = result.Detail,
            XmlAsserts = result.XmlAsserts,
            CompiledAsserts = result.CompiledAsserts,
        };

        public OracleResult ToResult() => new(Prefab, Outcome, Differences, Detail, XmlAsserts, CompiledAsserts);
    }

    /// <summary>
    /// Evaluates the specified prefabs across child worker chunk processes for the given oracle <paramref name="kind"/> and module set.
    /// </summary>
    public static (List<OracleResult> Results, List<string> Chunks) Run(string kind, IReadOnlyList<string> modules, IReadOnlyList<string> prefabs)
    {
        var parallel = int.TryParse(Environment.GetEnvironmentVariable("UIEXTENDEREX_ORACLE_PARALLEL"), out var parallelism) && parallelism > 0 ? parallelism : DefaultParallelism(kind);
        var chunkSize = int.TryParse(Environment.GetEnvironmentVariable("UIEXTENDEREX_ORACLE_CHUNK"), out var size) && size > 0 ? size : DefaultChunkSize(kind, prefabs.Count, parallel);
        // Divides the total memory limit across concurrent worker processes.
        var ceiling = OracleMemoryGuard.LimitMegabytes / parallel;
        var directory = Path.Combine(Path.GetTempPath(), "UIExtenderEx-oracle", "chunks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        // Distributes prefabs across chunks round-robin to balance execution duration across workers.
        var count = (prefabs.Count + chunkSize - 1) / chunkSize;
        var chunks = Enumerable.Range(0, count)
            .Select(x => prefabs.Where((_, i) => i % count == x).ToList())
            .ToList();
        var results = new List<OracleResult>[chunks.Count];
        var lines = new string[chunks.Count];
        Parallel.For(0, chunks.Count, new ParallelOptions { MaxDegreeOfParallelism = parallel }, index =>
        {
            var chunk = chunks[index];
            var stopwatch = Stopwatch.StartNew();
            var report = RunChunk(kind, modules, chunk, ceiling, Path.Combine(directory, $"chunk{index}"));
            var chunkResults = report.Results.Select(x => x.ToResult()).ToList();
            var outcome = "";
            if (report.Failure is not null)
            {
                var reported = report.Results.Select(x => x.Prefab).ToHashSet(StringComparer.Ordinal);
                var retried = Retry(kind, modules, [.. chunk.Where(x => !reported.Contains(x))], ceiling, Path.Combine(directory, $"chunk{index}"), report.Failure, failedAlone: chunk.Count == 1);
                chunkResults.AddRange(retried);
                var lost = retried.Count(x => x.Outcome == OracleOutcome.HarnessFailed);
                outcome = lost > 0
                    ? $", failed: {FirstLine(report.Failure)} ({lost} prefabs still failed on their own)"
                    : $", recovered in smaller chunks after: {FirstLine(report.Failure)}";
            }
            results[index] = chunkResults;
            lines[index] = $"chunk {index}: {chunk.Count} prefabs in {stopwatch.Elapsed.TotalSeconds:F0} s, peak {report.PeakMegabytes} MB of {ceiling}{outcome}";
            lock (lines)
                TestContext.Progress.WriteLine($"oracle {kind}: {lines[index]}");
        });
        return ([.. results.SelectMany(x => x)], [.. lines]);
    }

    /// <summary>
    /// Retries failed chunk prefabs by recursively bisecting the batch into smaller worker processes down to single prefabs.
    /// </summary>
    /// <param name="failedAlone"><see langword="true"/> if the prefab failed in an isolated single-item worker process.</param>
    private static List<OracleResult> Retry(string kind, IReadOnlyList<string> modules, List<string> prefabs, long ceilingMegabytes, string basePath, string failure, bool failedAlone)
    {
        if (prefabs.Count == 0)
            return [];
        if (failedAlone)
            return [.. prefabs.Select(x => new OracleResult(x, OracleOutcome.HarnessFailed, [], "chunk process, alone: " + failure, 0, 0))];

        var results = new List<OracleResult>();
        var groups = prefabs.Count == 1 ? [prefabs] : new[] { prefabs.Take(prefabs.Count / 2).ToList(), prefabs.Skip(prefabs.Count / 2).ToList() };
        for (var i = 0; i < groups.Length; i++)
        {
            var path = $"{basePath}-r{i}";
            var report = RunChunk(kind, modules, groups[i], ceilingMegabytes, path);
            results.AddRange(report.Results.Select(x => x.ToResult()));
            if (report.Failure is null)
                continue;
            var reported = report.Results.Select(x => x.Prefab).ToHashSet(StringComparer.Ordinal);
            results.AddRange(Retry(kind, modules, [.. groups[i].Where(x => !reported.Contains(x))], ceilingMegabytes, path, report.Failure, failedAlone: groups[i].Count == 1));
        }
        return results;
    }

    /// <summary>
    /// Computes default parallelism based on processor count and oracle workload memory profiles.
    /// </summary>
    private static int DefaultParallelism(string kind) => Math.Min(kind == "static" ? 3 : 2, Environment.ProcessorCount);

    /// <summary>
    /// Computes default chunk sizes balancing per-process startup overhead against working set memory growth.
    /// </summary>
    private static int DefaultChunkSize(string kind, int prefabs, int parallel) =>
        kind == "static" ? Math.Max(1, (prefabs + parallel - 1) / parallel) : 40;

    private static ChunkReport RunChunk(string kind, IReadOnlyList<string> modules, List<string> prefabs, long ceilingMegabytes, string basePath)
    {
        var resultsPath = basePath + ".json";
        var logPath = basePath + ".log";
        var testAssembly = typeof(OracleChunks).Assembly.Location;
        var start = new ProcessStartInfo("dotnet",
            $"test \"{testAssembly}\" --filter \"FullyQualifiedName={typeof(OracleWorkerTests).FullName}.{nameof(OracleWorkerTests.Worker)}\" --logger \"console;verbosity=normal\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        // Strips parent test host environment variables to isolate child process execution.
        foreach (var key in start.EnvironmentVariables.Keys.Cast<string>().Where(x => x.StartsWith("VSTEST", StringComparison.OrdinalIgnoreCase)).ToList())
            start.EnvironmentVariables.Remove(key);
        start.EnvironmentVariables[WorkerVariable] = "1";
        start.EnvironmentVariables[KindVariable] = kind;
        start.EnvironmentVariables[ModulesVariable] = string.Join(",", modules);
        start.EnvironmentVariables[PrefabsVariable] = string.Join(",", prefabs);
        start.EnvironmentVariables[ResultsVariable] = resultsPath;
        start.EnvironmentVariables["UIEXTENDEREX_ORACLE_MAX_MEMORY_MB"] = ceilingMegabytes.ToString();

        using var process = Process.Start(start)!;
        var output = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit((int) ChunkTimeLimit.TotalMilliseconds))
        {
            KillTree(process);
            File.WriteAllText(logPath, output.ToString());
            return new ChunkReport { Failure = $"did not finish in {ChunkTimeLimit.TotalMinutes} minutes and was killed; log {logPath}" };
        }
        process.WaitForExit();
        File.WriteAllText(logPath, output.ToString());

        if (!File.Exists(resultsPath))
            return new ChunkReport { Failure = $"exited with {process.ExitCode} and wrote no results; log {logPath}" };
        return JsonConvert.DeserializeObject<ChunkReport>(File.ReadAllText(resultsPath)) ?? new ChunkReport { Failure = "unreadable results " + resultsPath };
    }

    /// <summary>
    /// Terminates the process tree to ensure child test host processes are cleaned up.
    /// </summary>
    private static void KillTree(Process process)
    {
        try
        {
            using var killer = Process.Start(new ProcessStartInfo("taskkill", $"/T /F /PID {process.Id}") { UseShellExecute = false, CreateNoWindow = true });
            killer?.WaitForExit(10_000);
        }
        catch (Exception)
        {
            try { process.Kill(); }
            catch (Exception) { /* gone already */ }
        }
    }

    private static string FirstLine(string text) => text.Split('\n')[0].Trim();
}

/// <summary>
/// Executes a worker chunk invocation when launched within a child worker process spawned by <see cref="OracleChunks"/>.
/// </summary>
[NonParallelizable]
[Category("Corpus")]
public class OracleWorkerTests
{
    [Test]
    public void Worker()
    {
        if (Environment.GetEnvironmentVariable(OracleChunks.WorkerVariable) != "1")
        {
            Assert.Ignore("Runs only as a chunk of an oracle corpus run.");
            return;
        }
        var resultsPath = Environment.GetEnvironmentVariable(OracleChunks.ResultsVariable)!;
        var report = new OracleChunks.ChunkReport();
        using var watchdog = OracleMemoryGuard.StartWatchdog(reason =>
        {
            // Writes failure report to output file on watchdog termination.
            using var process = Process.GetCurrentProcess();
            File.WriteAllText(resultsPath, JsonConvert.SerializeObject(new OracleChunks.ChunkReport
            {
                Failure = reason,
                PeakMegabytes = process.PeakWorkingSet64 / (1024 * 1024),
            }));
        });
        try
        {
            var directory = GameInstallation.Find() ?? throw new InvalidOperationException("No game installation found.");
            var modules = Environment.GetEnvironmentVariable(OracleChunks.ModulesVariable)!.Split([','], StringSplitOptions.RemoveEmptyEntries);
            var prefabs = Environment.GetEnvironmentVariable(OracleChunks.PrefabsVariable)!.Split([','], StringSplitOptions.RemoveEmptyEntries);
            var kind = Environment.GetEnvironmentVariable(OracleChunks.KindVariable);
            var bindings = kind is "bindings" or "bindings-object";

            var game = GameInstallation.Load(directory, modules);
            var widgetAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => !x.IsDynamic && !string.IsNullOrEmpty(x.Location) && x.Location.StartsWith(directory, StringComparison.OrdinalIgnoreCase));
            var results = bindings
                ? new BindingLoaderOracle(game.BindingWidgetFactory, game.SpriteData, game.BrushFactory, game.CreateUIContext, widgetAssemblies, objectTyped: kind == "bindings-object").Run(prefabs)
                : new LoaderOracle(game.WidgetFactory, game.SpriteData, game.BrushFactory, game.CreateUIContext(), widgetAssemblies).Run(prefabs);
            report.Results = [.. results.Select(OracleChunks.ResultRecord.From)];
        }
        catch (Exception e)
        {
            report.Failure = $"{e.GetType().Name}: {e.Message}";
        }
        using (var process = Process.GetCurrentProcess())
            report.PeakMegabytes = process.PeakWorkingSet64 / (1024 * 1024);
        File.WriteAllText(resultsPath, JsonConvert.SerializeObject(report));
    }
}