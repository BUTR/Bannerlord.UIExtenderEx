using Bannerlord.UIExtenderEx.CompiledPrefabs;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Measures total execution duration for <see cref="GauntletMovie.Load"/> invocations and records execution metrics into <see cref="PrefabTimings"/>.
/// <para>
/// Installs high-priority prefix and low-priority postfix hooks to measure movie instantiation latency across both XML parsing
/// and compiled prefab execution paths.
/// </para>
/// </summary>
public static class GauntletMovieTimingPatch
{
    /// <summary>Applies Harmony prefix and postfix patches to <see cref="GauntletMovie.Load"/>.</summary>
    public static void Patch(Harmony harmony)
    {
        try
        {
            // By type rather than by name: the runtime is installed when the SubModule is built, and the assembly may not be
            // loaded yet, which a lookup by name does not change
            if (AccessTools2.DeclaredMethod(typeof(GauntletMovie), nameof(GauntletMovie.Load)) is not { } method)
            {
                Trace.TraceWarning("UIExtenderEx: GauntletMovie.Load was not found, movie loads are not timed");
                return;
            }
            harmony.Patch(method,
                prefix: new HarmonyMethod(AccessTools2.DeclaredMethod(typeof(GauntletMovieTimingPatch), nameof(LoadPrefix)), Priority.First),
                postfix: new HarmonyMethod(AccessTools2.DeclaredMethod(typeof(GauntletMovieTimingPatch), nameof(LoadPostfix)), Priority.Last));
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: GauntletMovie.Load could not be patched, movie loads are not timed: {0}", e.Message);
        }
    }

    private static void LoadPrefix(out long __state) => __state = Stopwatch.GetTimestamp();

    /// <remarks>
    /// Inspects the instantiated <see cref="IGauntletMovie"/> and root widget assembly to determine whether the UI was served
    /// by raw XML parsing, game pre-generated code, or UIExtenderEx compiled prefabs.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoadPostfix(string movieName, IViewModel? datasource, IGauntletMovie? __result, long __state)
    {
        try
        {
            var milliseconds = (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency;
            var servedBy = __result switch
            {
                null => "nothing",
                GauntletMovie => "XML",
                _ when __result.RootWidget is { } root && CompiledPrefabManager.IsGeneratedAssembly(root.GetType().Assembly) => "compiled",
                _ => "game's generated",
            };
            // The variant name GauntletMovie.Load looks a generated prefab up by
            CompiledPrefabRuntime.Manager.RecordMovieLoad(movieName, datasource?.GetType().FullName ?? "Default", milliseconds, servedBy);
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: could not time the load of '{0}': {1}", movieName, e.Message);
        }
    }
}