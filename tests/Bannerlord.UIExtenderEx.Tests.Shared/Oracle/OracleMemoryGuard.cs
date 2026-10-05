using System;
using System.Diagnostics;
using System.Threading;

namespace Bannerlord.UIExtenderEx.Tests.Oracle;

/// <summary>
/// Enforces an absolute memory limit on oracle test execution, evaluated prior to each prefab test pass.
/// <para>
/// Prevents unconstrained memory growth from dynamically generated and loaded assemblies on runtime platforms lacking collectible assembly unload support.
/// Configure the threshold via the <c>UIEXTENDEREX_ORACLE_MAX_MEMORY_MB</c> environment variable.
/// </para>
/// </summary>
public static class OracleMemoryGuard
{
    private const long DefaultLimitMegabytes = 3072;

    public static long LimitMegabytes =>
        long.TryParse(Environment.GetEnvironmentVariable("UIEXTENDEREX_ORACLE_MAX_MEMORY_MB"), out var limit) && limit > 0 ? limit : DefaultLimitMegabytes;

    /// <summary>Verifies that committed process memory remains below the configured ceiling, throwing <see cref="OracleMemoryExceededException"/> upon violation.</summary>
    /// <param name="where">Diagnostic string describing the active test step.</param>
    public static void Check(string where)
    {
        At(where);
        var megabytes = CurrentMegabytes();
        // Executes garbage collection prior to threshold evaluation to measure retained memory rather than transient allocation garbage.
        if (megabytes > LimitMegabytes)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            megabytes = CurrentMegabytes();
        }
        if (megabytes > LimitMegabytes)
        {
            throw new OracleMemoryExceededException(
                $"The oracle run stopped at {where}: the process holds {megabytes} MB, over the {LimitMegabytes} MB ceiling " +
                "(UIEXTENDEREX_ORACLE_MAX_MEMORY_MB). Run fewer prefabs at once (UIEXTENDEREX_ORACLE_PREFABS).");
        }
    }

    private static volatile string _where = "starting";

    private static readonly bool Trace = Environment.GetEnvironmentVariable("UIEXTENDEREX_ORACLE_TRACE") == "1";

    /// <summary>
    /// Records the current test execution location for diagnostic tracking. Appends to disk when <c>UIEXTENDEREX_ORACLE_TRACE=1</c> is active.
    /// </summary>
    public static void At(string where)
    {
        _reading = false;
        _where = where;
        // Writes synchronously to disk to preserve trace records across fatal crashes.
        if (Trace)
            System.IO.File.AppendAllText(TracePath, where + Environment.NewLine);
    }

    private static volatile bool _reading;
    private static string _readingWidget = "", _readingPath = "", _readingProperty = "", _readingType = "";

    /// <summary>
    /// Records granular property snapshot traversal state for watchdog diagnostic tracking.
    /// </summary>
    public static void Reading(string widgetPath, string path, string property, string type)
    {
        if (Trace)
        {
            At($"reading {widgetPath}{path}.{property} ({type})");
            return;
        }
        _readingWidget = widgetPath;
        _readingPath = path;
        _readingProperty = property;
        _readingType = type;
        _reading = true;
    }

    /// <summary>Gets the formatted current execution position for diagnostics.</summary>
    private static string Where => _reading ? $"reading {_readingWidget}{_readingPath}.{_readingProperty} ({_readingType})" : _where;

    /// <summary>Gets the diagnostic trace file path used when <c>UIEXTENDEREX_ORACLE_TRACE=1</c> is enabled.</summary>
    public static string TracePath { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UIExtenderEx-oracle", $"trace-{Process.GetCurrentProcess().Id}.txt");

    /// <summary>
    /// Starts a background timer watchdog to terminate runaway memory growth during long-running individual prefab operations.
    /// </summary>
    public static IDisposable StartWatchdog(Action<string> onExceeded)
    {
        var fired = 0;
        return new Timer(_ =>
        {
            var megabytes = CurrentMegabytes();
            if (megabytes <= LimitMegabytes)
                return;
            // Performs explicit garbage collection to verify that retained memory exceeds the threshold before terminating.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            megabytes = CurrentMegabytes();
            if (megabytes <= LimitMegabytes || Interlocked.Exchange(ref fired, 1) != 0)
                return;
            try
            {
                onExceeded($"The watchdog killed the process at {megabytes} MB, over the {LimitMegabytes} MB ceiling " +
                           $"(UIEXTENDEREX_ORACLE_MAX_MEMORY_MB), at {Where}.");
            }
            finally
            {
                using var process = Process.GetCurrentProcess();
                process.Kill();
            }
        }, null, 0, 200);
    }

    private static long CurrentMegabytes()
    {
        using var process = Process.GetCurrentProcess();
        return process.PrivateMemorySize64 / (1024 * 1024);
    }
}

/// <summary>Represents an unhandled exception thrown when memory consumption exceeds the configured ceiling.</summary>
internal sealed class OracleMemoryExceededException(string message) : Exception(message);