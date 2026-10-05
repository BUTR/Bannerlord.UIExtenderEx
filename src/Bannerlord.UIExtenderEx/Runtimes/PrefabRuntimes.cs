using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Bannerlord.UIExtenderEx.Runtimes;

/// <summary>
/// Coordinates registered prefab runtimes and exposes shared runtime capabilities.
/// <para>
/// When no runtimes are registered, all movies fall back to the native Gauntlet XML loader with all patches applied.
/// Registered runtimes act strictly as performance optimizations, serving pre-compiled or dynamically compiled widget
/// trees where applicable.
/// </para>
/// </summary>
public static class PrefabRuntimes
{
    private static readonly object RegistrationLock = new();

    /// <summary>
    /// Read during every movie load. Stored as an immutable, published array so readers observe consistent snapshots
    /// without lock contention or allocations during iteration.
    /// </summary>
    private static IPrefabRuntime[] _runtimes = [];

    /// <summary>
    /// Registers an <see cref="IPrefabRuntime"/> implementation. Runtimes are evaluated in registration order
    /// (<c>SubModule.xml</c> loading order), allowing the game runtime to inspect native variants prior to triggering
    /// background compilation. Registering an existing runtime has no effect.
    /// </summary>
    public static void Register(IPrefabRuntime runtime)
    {
        if (runtime is null)
            throw new ArgumentNullException(nameof(runtime));

        lock (RegistrationLock)
        {
            if (_runtimes.Contains(runtime))
                return;
            Volatile.Write(ref _runtimes, [.. _runtimes, runtime]);
        }
        Trace.TraceInformation("UIExtenderEx: prefab runtime '{0}' registered", runtime.GetType().FullName);
    }

    internal static IPrefabRuntime[] All => Volatile.Read(ref _runtimes);

    /// <summary>Determines whether any registered runtime claims ownership of the specified variant assembly.</summary>
    public static bool IsRuntimeVariant(Assembly variantAssembly)
    {
        foreach (var runtime in All)
        {
            if (runtime.IsOwnVariant(variantAssembly))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets or sets whether the Harmony prefix on <c>GauntletMovie.Load</c> is active. The switch coordinates variant
    /// selection across runtimes; if absent, runtimes must not register or preload compiled variants.
    /// </summary>
    public static bool IsMovieSwitchInstalled { get; internal set; }

    /// <summary>
    /// Gets or sets whether the XML loader resolves dotted attribute paths exceeding two segments (enabled via the XML
    /// runtime's patch). Runtimes constructing widgets independently mirror this path resolution logic.
    /// </summary>
    public static bool DottedAttributePathsResolve { get; set; }

    /// <summary>
    /// Temporarily overrides registered runtimes with a test-provided set, restoring previous state upon disposal.
    /// </summary>
    internal static IDisposable ResetForTests(params IPrefabRuntime[] runtimes)
    {
        IPrefabRuntime[] previous;
        lock (RegistrationLock)
        {
            previous = _runtimes;
            Volatile.Write(ref _runtimes, [.. runtimes]);
        }
        return new Restore(previous);
    }

    private sealed class Restore(IPrefabRuntime[] previous) : IDisposable
    {
        public void Dispose()
        {
            lock (RegistrationLock)
                Volatile.Write(ref _runtimes, previous);
        }
    }
}