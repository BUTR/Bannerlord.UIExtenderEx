using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

namespace Bannerlord.UIExtenderEx.Patches;

internal static class UIConfigPatch
{
    public static void Patch(Harmony harmony)
    {
        harmony.TryPatch(
            AccessTools2.DeclaredPropertySetter("TaleWorlds.Engine.GauntletUI.UIConfig:DoNotUseGeneratedPrefabs"),
            prefix: AccessTools2.DeclaredMethod("Bannerlord.UIExtenderEx.Patches.UIConfigPatch:Prefix"));
    }

    // Disable setting a value to DoNotUseGeneratedPrefabs
    private static bool Prefix() => false;
}