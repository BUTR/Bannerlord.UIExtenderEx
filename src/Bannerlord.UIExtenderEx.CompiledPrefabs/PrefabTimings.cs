using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Records performance metrics for prefab loading, compilation, and warm-up operations across game sessions.
/// <para>
/// Persists tab-separated log files into the <see cref="DirectoryName"/> subfolder within the compiled prefabs cache directory.
/// Offloads file writing to background worker threads to eliminate main thread I/O latency.
/// </para>
/// <para>
/// Automatically rotates log files to retain a configurable number of historical session logs (<see cref="KeptSessions"/>).
/// </para>
/// </summary>
public sealed class PrefabTimings
{
    /// <summary>The directory name where session timing log files are stored.</summary>
    public const string DirectoryName = "Timings";

    /// <summary>The file extension for tab-separated timing files.</summary>
    public const string FileExtension = ".tsv";

    /// <summary>The column headers written to the first line of the timing log file.</summary>
    public const string Header = "time\tthread\tevent\tmovie\tvariant\tms\tdetail";

    /// <summary>The event name recorded when the log line capacity is exceeded.</summary>
    public const string TruncatedEvent = "truncated";

    private readonly Action<Action> _runInBackground;
    private readonly ConcurrentQueue<string> _pending = new();
    private readonly object _writeLock = new();
    private int _recorded;
    private int _flushScheduled;
    private bool _started;
    private bool _failed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrefabTimings"/> class for the current game session.
    /// </summary>
    /// <param name="directory">The root cache directory.</param>
    /// <param name="sessionStart">The UTC start timestamp for the current session.</param>
    /// <param name="processId">The operating system process identifier.</param>
    /// <param name="runInBackground">Delegate used to dispatch background disk writes.</param>
    /// <param name="keptSessions">The number of historical session log files to retain.</param>
    /// <param name="maxLines">The maximum number of metric lines recorded per session.</param>
    public PrefabTimings(string directory, DateTime sessionStart, int processId, Action<Action> runInBackground, int keptSessions = 10, int maxLines = 5000)
    {
        DirectoryPath = Path.Combine(directory, DirectoryName);
        FilePath = Path.Combine(DirectoryPath, $"{sessionStart.ToUniversalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{processId.ToString(CultureInfo.InvariantCulture)}{FileExtension}");
        _runInBackground = runInBackground;
        KeptSessions = Math.Max(1, keptSessions);
        MaxLines = Math.Max(1, maxLines);
    }

    public string DirectoryPath { get; }

    /// <summary>Gets the absolute file path of the current session's log file.</summary>
    public string FilePath { get; }

    /// <summary>Gets the maximum count of session log files to retain.</summary>
    public int KeptSessions { get; }

    /// <summary>Gets the maximum count of metric lines to record in this session.</summary>
    public int MaxLines { get; }

    /// <summary>
    /// Records a timed event metric line. Thread-safe and returns immediately without blocking.
    /// </summary>
    /// <param name="what">The event description or operation name.</param>
    /// <param name="movie">The movie or prefab name.</param>
    /// <param name="variant">The ViewModel or variant identifier.</param>
    /// <param name="milliseconds">The duration of the operation in milliseconds.</param>
    /// <param name="detail">Optional diagnostic context details.</param>
    public void Record(string what, string? movie, string? variant, double milliseconds, string? detail = null)
    {
        try
        {
            if (_failed)
                return;

            var count = Interlocked.Increment(ref _recorded);
            if (count > MaxLines + 1)
                return;
            _pending.Enqueue(count == MaxLines + 1
                ? FormatLine(TruncatedEvent, null, null, 0, $"the session recorded {MaxLines} lines; later ones are not recorded")
                : FormatLine(what, movie, variant, milliseconds, detail));

            if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
                _runInBackground(FlushScheduled);
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }

    /// <summary>Flushes queued log lines to disk. Executed on a background worker thread.</summary>
    public void Flush()
    {
        try
        {
            lock (_writeLock)
            {
                if (_failed || _pending.IsEmpty)
                    return;

                var sb = new StringBuilder();
                if (!_started)
                {
                    Start();
                    if (!File.Exists(FilePath))
                        sb.Append(Header).Append('\n');
                    _started = true;
                }
                while (_pending.TryDequeue(out var line))
                    sb.Append(line).Append('\n');

                // Shared for reading, so the file can be looked at while the game runs
                using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                stream.Write(bytes, 0, bytes.Length);
            }
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }

    private void FlushScheduled()
    {
        // Cleared first: a line recorded while this writes schedules the next write
        Volatile.Write(ref _flushScheduled, 0);
        Flush();
    }

    /// <summary>
    /// Creates the log directory and purges historical session files beyond <see cref="KeptSessions"/>.
    /// </summary>
    private void Start()
    {
        Directory.CreateDirectory(DirectoryPath);
        var older = Directory.GetFiles(DirectoryPath, "*" + FileExtension)
            .Where(x => !string.Equals(x, FilePath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => Path.GetFileName(x), StringComparer.Ordinal)
            .Skip(KeptSessions - 1);
        foreach (var file in older)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Another game instance may have it open; it goes next time
            }
        }
    }

    private void Fail(Exception e)
    {
        if (_failed)
            return;
        _failed = true;
        Trace.TraceWarning("UIExtenderEx: compiled prefab timings cannot be written to '{0}', not recording them for the rest of the session: {1}", FilePath, e.Message);
    }

    private static string FormatLine(string what, string? movie, string? variant, double milliseconds, string? detail) =>
        string.Join("\t",
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture),
            Clean(what),
            Clean(movie),
            Clean(variant),
            milliseconds.ToString("0.###", CultureInfo.InvariantCulture),
            Clean(detail));

    /// <summary>Sanitizes text fields by replacing tabs and line break characters with whitespace.</summary>
    private static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        var sb = new StringBuilder(text!.Length);
        foreach (var c in text)
            sb.Append(c is '\t' or '\r' or '\n' ? ' ' : c);
        return sb.ToString();
    }
}