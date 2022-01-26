using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

namespace Bannerlord.UIExtenderEx.Patches
{
    internal static class UIConfigPatch
    {
        public static void Patch(Harmony harmony)
        {
            harmony.TryPatch(
                AccessTools2.PropertySetter("TaleWorlds.Engine.GauntletUI.UIConfig:DoNotUseGeneratedPrefabs"),
                prefix: AccessTools2.Method(typeof(UIConfigPatch), nameof(Prefix)));
        }

        // Disable setting a value to DoNotUseGeneratedPrefabs
        private static bool Prefix() => false;
    }
}