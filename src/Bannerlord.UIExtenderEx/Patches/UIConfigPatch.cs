using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Reflection;

namespace Bannerlord.UIExtenderEx.Patches
{
    internal static class UIConfigPatch
    {
        private delegate void SetDoNotUseGeneratedPrefabsDelegate(bool value);

        public static void Patch(Harmony harmony)
        {
            try
            {
                // Force load TaleWorlds.Engine.GauntletUI
                Assembly.Load("TaleWorlds.Engine.GauntletUI");
            }
            catch (Exception e)
            {
                Utils.Fail("Failed to load 'TaleWorlds.Engine.GauntletUI'!");
            }

            var setDoNotUseGeneratedPrefabs = AccessTools2.GetPropertySetterDelegate<SetDoNotUseGeneratedPrefabsDelegate>("TaleWorlds.Engine.GauntletUI.UIConfig:DoNotUseGeneratedPrefabs");
            if (setDoNotUseGeneratedPrefabs is not null)
                setDoNotUseGeneratedPrefabs(true);

            harmony.TryPatch(
                AccessTools2.PropertySetter("TaleWorlds.Engine.GauntletUI.UIConfig:DoNotUseGeneratedPrefabs"),
                prefix: AccessTools2.Method(typeof(GauntletMoviePatch), nameof(Prefix)));
        }

        // Disable setting a value to DoNotUseGeneratedPrefabs
        private static bool Prefix() => false;
    }
}