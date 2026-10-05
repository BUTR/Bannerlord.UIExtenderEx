using Bannerlord.UIExtenderEx.Runtimes;
using Bannerlord.UIExtenderEx.Utils;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Diagnostics;
using System.Linq;

using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Intercepts <c>GauntletMovie.Load</c> to evaluate whether a movie can be served by a registered prefab runtime or must fall back to the XML loader.
/// <para>
/// TaleWorlds checks <see cref="WidgetFactory.GeneratedPrefabContext"/> for pre-compiled widget classes. This patch queries
/// each registered <see cref="IPrefabRuntime"/> in sequence (<see cref="PrefabRuntimes.All"/>). If no runtime vouches for a
/// compiled variant, <c>doNotUseGeneratedPrefabs</c> is forced to <see langword="true"/>, guaranteeing that all mod XML patches
/// are applied via the XML loader.
/// </para>
/// </summary>
internal static class GauntletMoviePatch
{
    public static void Patch(Harmony harmony)
    {
        if (AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.Data.GauntletMovie:Load") is { } mi && mi.GetParameters() is { } p && p.Any(x => x.Name == "doNotUseGeneratedPrefabs") &&
            harmony.TryPatch(mi, prefix: AccessTools2.DeclaredMethod(typeof(GauntletMoviePatch), nameof(LoadPrefix))))
        {
            PrefabRuntimes.IsMovieSwitchInstalled = true;
            return;
        }

        MessageUtils.DisplayUserWarning(
            "Failed to patch GauntletMovie.Load! Changes mods make to some of the game's screens will not appear, and screens that mods change may open more slowly.");
    }

    internal static void LoadPrefix(WidgetFactory widgetFactory, string movieName, IViewModel? datasource, ref bool doNotUseGeneratedPrefabs)
    {
        // The game already decided against pre-compiled prefabs
        if (doNotUseGeneratedPrefabs)
            return;

        foreach (var runtime in PrefabRuntimes.All)
        {
            if (TryServe(runtime, widgetFactory, movieName, datasource))
                return;
        }

        // If no runtime claims the movie, fall back to the XML loader with all patches applied
        doNotUseGeneratedPrefabs = true;
    }

    /// <summary>
    /// Invokes <see cref="IPrefabRuntime.TryServe"/> within a guarded block, catching exceptions to prevent engine movie load failures.
    /// </summary>
    private static bool TryServe(IPrefabRuntime runtime, WidgetFactory widgetFactory, string movieName, IViewModel? datasource)
    {
        try
        {
            return runtime.TryServe(widgetFactory, movieName, datasource);
        }
        catch (Exception e)
        {
            Trace.TraceError("UIExtenderEx: prefab runtime '{0}' failed on movie '{1}': {2}", runtime.GetType().FullName, movieName, e);
            return false;
        }
    }
}