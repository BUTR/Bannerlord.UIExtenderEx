using Bannerlord.UIExtenderEx.CompiledPrefabs;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Runtime.CompilerServices;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Re-registers compiled prefab factory creators into <see cref="GeneratedPrefabContext"/> whenever the engine reloads UI resources.
/// <para>
/// When the game refreshes UI resources, it flushes registered prefab factories and scans loaded assemblies.
/// This patch re-injects compiled prefab variants following the scan.
/// </para>
/// </summary>
public static class GeneratedPrefabContextPatch
{
    /// <summary>Applies a Harmony postfix patch to <see cref="GeneratedPrefabContext.CollectPrefabs"/>.</summary>
    public static void Patch(Harmony harmony)
    {
        // Without it a resource refresh drops the compiled variants, and the movies they served load from XML until rebuilt
        if (!harmony.TryPatch(
                AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext:CollectPrefabs"),
                postfix: AccessTools2.DeclaredMethod(typeof(GeneratedPrefabContextPatch), nameof(CollectPrefabsPostfix))))
        {
            MessageUtils.DisplayUserWarning("Failed to patch GeneratedPrefabContext.CollectPrefabs! After the game reloads its UI, screens that mods change may open more slowly.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectPrefabsPostfix(GeneratedPrefabContext __instance) => CompiledPrefabRuntime.Manager.OnPrefabsCollected(__instance);
}