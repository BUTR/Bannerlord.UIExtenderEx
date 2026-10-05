using System;
using System.Threading;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Tracks structural changes in the UI environment, including assembly loading, widget and prefab registrations, and mixin state transitions.
/// <para>
/// <see cref="PrefabReferenceSet"/> scans loaded assemblies to assemble compilation references. Incrementing this monotonic version
/// counter signals when cached compilation snapshots must be invalidated and re-evaluated.
/// </para>
/// </summary>
public static class UIEnvironmentVersion
{
    private static int _version;

    static UIEnvironmentVersion()
    {
        // Assemblies loaded dynamically at runtime can introduce new widget types, view models, or mixins.
        AppDomain.CurrentDomain.AssemblyLoad += (_, e) =>
        {
            if (PrefabReferenceSet.CanReference(e.LoadedAssembly))
                Touch();
        };
    }

    /// <summary>Gets the current monotonic environment version counter.</summary>
    public static int Version => Volatile.Read(ref _version);

    /// <summary>Increments the environment version counter to invalidate cached references.</summary>
    public static void Touch() => Interlocked.Increment(ref _version);
}