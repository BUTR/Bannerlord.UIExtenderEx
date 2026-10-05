using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Patches;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies diagnostic session timing serialization, disk retention limits, sanitization, and non-intrusive logging failure modes.
/// </summary>
public class PrefabTimingsTests
{
    private static readonly DateTime Session = new(2026, 10, 4, 12, 30, 15, DateTimeKind.Utc);

    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "UIExtenderEx-timings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_directory, true); } catch (Exception) { /* ignore */ }
    }

    private PrefabTimings Create(DateTime? start = null, int keptSessions = 10, int maxLines = 5000, Action<Action>? runInBackground = null) =>
        new(_directory, start ?? Session, 4242, runInBackground ?? (x => x()), keptSessions, maxLines);

    private static string[][] Lines(PrefabTimings timings) =>
        File.ReadAllLines(timings.FilePath).Skip(1).Select(x => x.Split('\t')).ToArray();

    /// <summary>
    /// Verifies that each timing session generates an isolated diagnostic file named after its startup timestamp.
    /// </summary>
    [Test]
    public void ASession_IsAFileOfItsOwn_NamedForItsStart()
    {
        var timings = Create();

        timings.Record("load movie", "SomeMovie", "Some.ViewModel", 12.5, "XML");

        Assert.That(timings.FilePath, Is.EqualTo(Path.Combine(_directory, PrefabTimings.DirectoryName, "20261004-123015-4242" + PrefabTimings.FileExtension)));
        Assert.That(File.ReadAllLines(timings.FilePath)[0], Is.EqualTo(PrefabTimings.Header));
    }

    /// <summary>
    /// Verifies that each recorded timing metric outputs as a tab-delimited entry matching the standard header schema.
    /// </summary>
    [Test]
    public void EachRecord_IsALineOfTheHeadersColumns()
    {
        var timings = Create();

        timings.Record("load movie", "SomeMovie", "Some.ViewModel", 12.5, "XML");
        timings.Record("warm-up compiler", null, null, 1500, "Roslyn");

        var lines = Lines(timings);
        Assert.That(lines, Has.Length.EqualTo(2));
        Assert.That(lines.Select(x => x.Length), Is.All.EqualTo(PrefabTimings.Header.Split('\t').Length));
        Assert.That(lines[0].Skip(2), Is.EqualTo(new[] { "load movie", "SomeMovie", "Some.ViewModel", "12.5", "XML" }));
        Assert.That(lines[1].Skip(2), Is.EqualTo(new[] { "warm-up compiler", "", "", "1500", "Roslyn" }));
        Assert.That(DateTime.TryParse(lines[0][0], out _), Is.True, "the time it was recorded");
    }

    /// <summary>
    /// Verifies that tabs and newline characters within message payloads are sanitized to prevent splitting TSV record lines.
    /// </summary>
    [Test]
    public void ATabOrLineBreakInAField_DoesNotSplitTheLine()
    {
        var timings = Create();

        timings.Record("decide", "Some\tMovie", null, 1, "generating failed (line one\r\nline two), XML");

        var lines = Lines(timings);
        Assert.That(lines, Has.Length.EqualTo(1));
        Assert.That(lines[0][3], Is.EqualTo("Some Movie"));
        Assert.That(lines[0][6], Is.EqualTo("generating failed (line one  line two), XML"));
    }

    /// <summary>
    /// Verifies that recording calls queue work asynchronously and batch disk writes on a background worker thread.
    /// </summary>
    [Test]
    public void Recording_WritesOnTheWorker_OncePerBatch()
    {
        var background = new Queue<Action>();
        var timings = Create(runInBackground: background.Enqueue);

        timings.Record("load movie", "A", null, 1);
        timings.Record("load movie", "B", null, 2);

        Assert.That(File.Exists(timings.FilePath), Is.False, "nothing written on the recording thread");
        Assert.That(background, Has.Count.EqualTo(1), "a second line joins the write already scheduled");
        background.Dequeue()();
        Assert.That(Lines(timings).Select(x => x[3]), Is.EqualTo(new[] { "A", "B" }));

        timings.Record("load movie", "C", null, 3);
        Assert.That(background, Has.Count.EqualTo(1), "a line after the write schedules the next one");
        background.Dequeue()();
        Assert.That(Lines(timings).Select(x => x[3]), Is.EqualTo(new[] { "A", "B", "C" }));
    }

    /// <summary>
    /// Verifies that sessions cease recording additional entries once reaching maximum configured line limits.
    /// </summary>
    [Test]
    public void ASession_StopsRecordingAtItsLimit_SayingSo()
    {
        var timings = Create(maxLines: 3);

        for (var i = 0; i < 10; i++)
            timings.Record("load movie", "Movie" + i, null, i);

        var lines = Lines(timings);
        Assert.That(lines.Select(x => x[2]), Is.EqualTo(new[] { "load movie", "load movie", "load movie", PrefabTimings.TruncatedEvent }));
    }

    /// <summary>
    /// Verifies that older session files are pruned to maintain only the configured count of most recent sessions.
    /// </summary>
    [Test]
    public void OnlyTheLastSessions_AreKept()
    {
        for (var i = 0; i < 12; i++)
        {
            var older = Create(Session.AddDays(-12 + i));
            older.Record("load movie", "Movie", null, i);
        }

        var timings = Create(keptSessions: 5);
        Assert.That(Directory.GetFiles(timings.DirectoryPath), Has.Length.EqualTo(10), "test premise: each session kept ten");
        timings.Record("load movie", "Movie", null, 1);

        var kept = Directory.GetFiles(timings.DirectoryPath).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.That(kept, Has.Count.EqualTo(5));
        Assert.That(kept.Last(), Is.EqualTo(Path.GetFileName(timings.FilePath)));
        Assert.That(kept.First(), Does.StartWith("20260930-"), "the four newest of the older sessions");
    }

    /// <summary>
    /// Verifies that multiple writers sharing identical session timestamps append entries rather than overwriting existing log files.
    /// </summary>
    [Test]
    public void ASecondWriterOfTheSameFile_Appends()
    {
        Create().Record("load movie", "A", null, 1);
        var again = Create();

        again.Record("load movie", "B", null, 2);

        Assert.That(Lines(again).Select(x => x[3]), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(File.ReadAllLines(again.FilePath).Count(x => x == PrefabTimings.Header), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that I/O write failures terminate logging gracefully without propagating exceptions to the host game.
    /// </summary>
    [Test]
    public void AFolderThatCannotBeWritten_EndsTheRecording_WithoutThrowing()
    {
        // Simulates an unwritable directory path by placing a blocking file.
        File.WriteAllText(Path.Combine(_directory, PrefabTimings.DirectoryName), "in the way");
        var timings = Create();

        Assert.That(() => timings.Record("load movie", "A", null, 1), Throws.Nothing);
        Assert.That(() => timings.Record("load movie", "B", null, 2), Throws.Nothing);
        Assert.That(() => timings.Flush(), Throws.Nothing);
        Assert.That(File.Exists(timings.FilePath), Is.False);
    }

    /// <summary>
    /// Verifies that resetting or invalidating compiled prefab caches does not delete timing session history.
    /// </summary>
    [Test]
    public void StartingTheCacheOver_LeavesTheTimingsAlone()
    {
        var timings = Create();
        timings.Record("load movie", "A", null, 1);
        new PrefabCache(_directory, "generation-1", []).Flush();

        _ = new PrefabCache(_directory, "generation-2", []);

        Assert.That(File.Exists(timings.FilePath), Is.True);
    }

    /// <summary>
    /// Verifies that <see cref="GauntletMovieTimingPatch"/> wraps <see cref="TaleWorlds.GauntletUI.Data.GauntletMovie.Load"/>
    /// with highest-priority prefix and lowest-priority postfix hooks to measure complete invocation duration.
    /// </summary>
    [Test]
    public void GauntletMovieLoad_IsTimedAroundEveryOtherPatch()
    {
        var load = AccessTools2.DeclaredMethod(typeof(TaleWorlds.GauntletUI.Data.GauntletMovie), nameof(TaleWorlds.GauntletUI.Data.GauntletMovie.Load));
        Assert.That(load, Is.Not.Null);
        var info = Harmony.GetPatchInfo(load);
        Assert.That(info, Is.Not.Null, "the compiled runtime is installed for these tests");

        // Compares declaring type full names to accommodate potential shadow-loaded runtime assemblies.
        var prefix = info!.Prefixes.SingleOrDefault(x => x.PatchMethod.DeclaringType?.FullName == typeof(GauntletMovieTimingPatch).FullName);
        var postfix = info.Postfixes.SingleOrDefault(x => x.PatchMethod.DeclaringType?.FullName == typeof(GauntletMovieTimingPatch).FullName);
        Assert.That(prefix, Is.Not.Null);
        Assert.That(postfix, Is.Not.Null);
        Assert.That(info.Prefixes.Where(x => x != prefix).Select(x => x.priority), Is.All.LessThan(prefix!.priority));
        Assert.That(info.Postfixes.Where(x => x != postfix).Select(x => x.priority), Is.All.GreaterThan(postfix!.priority));
    }
}
