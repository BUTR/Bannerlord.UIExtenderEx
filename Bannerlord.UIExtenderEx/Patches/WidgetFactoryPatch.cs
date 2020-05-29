using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Patches
{
    /// <summary>
    /// Skips type duplicates
    /// </summary>
    public static class WidgetFactoryPatch
    {
        private static AccessTools.FieldRef<WidgetFactory, Dictionary<string, Type>> BuiltinTypesField { get; } =
            AccessTools.FieldRefAccess<WidgetFactory, Dictionary<string, Type>>("_builtinTypes");
        private static MethodInfo GetPrefabNamesAndPathsFromCurrentPathMethod { get; } =
            AccessTools.Method(typeof(WidgetFactory), "GetPrefabNamesAndPathsFromCurrentPath");

        public static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(WidgetFactory), "Initialize");

        public static bool InitializePrefix(WidgetFactory __instance)
        {
            var builtinTypes = BuiltinTypesField(__instance);
            var prefabsData = GetPrefabNamesAndPathsFromCurrentPathMethod.Invoke(__instance, Array.Empty<object>()) as Dictionary<string, string>;
            if (builtinTypes == null || prefabsData == null)
                return true; // fallback

            foreach (var prefabExtension in __instance.PrefabExtensionContext.PrefabExtensions)
            {
                var method = AccessTools.Method(prefabExtension.GetType(), "RegisterAttributeTypes");
                method.Invoke(prefabExtension, new object[] { __instance.WidgetAttributeContext });
            }
            foreach (var type in WidgetInfo.CollectWidgetTypes())
            {
                if (!builtinTypes.ContainsKey(type.Name))
                    builtinTypes.Add(type.Name, type);
            }
            foreach (var keyValuePair in prefabsData)
            {
                __instance.AddCustomType(keyValuePair.Key, keyValuePair.Value);
            }

            return false;
        }
    }
}