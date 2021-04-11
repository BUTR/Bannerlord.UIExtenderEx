using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator
{
    internal readonly struct GeneratedPrefabContextHandle
    {
        private delegate object GetGeneratedPrefabContextDelegate(WidgetFactory instance);
        private static readonly GetGeneratedPrefabContextDelegate? GetGeneratedPrefabContext;

        private static readonly AccessTools.FieldRef<object, IDictionary>? GeneratedPrefabs;

        static GeneratedPrefabContextHandle()
        {
            if (AccessTools.PropertyGetter(typeof(WidgetFactory), "GeneratedPrefabContext") is { } propertyGetter)
            {
                GetGeneratedPrefabContext = AccessTools2.GetDelegate<GetGeneratedPrefabContextDelegate>(propertyGetter);
            }

            if (AccessTools.TypeByName("TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext") is { } generatedPrefabContextType)
            {
                GeneratedPrefabs = AccessTools2.FieldRefAccess<IDictionary>(generatedPrefabContextType, "_generatedPrefabs");
            }
        }

        public static GeneratedPrefabContextHandle? Get(WidgetFactory wf) => GetGeneratedPrefabContext?.Invoke(wf) is { } handle
            ? new GeneratedPrefabContextHandle(handle)
            : null;

        private readonly object _generatedPrefabContext;

        private GeneratedPrefabContextHandle(object generatedPrefabContext) => _generatedPrefabContext = generatedPrefabContext;

        public void OverridePrefab(object generatedUIPrefabCreator)
        {
            var method = AccessTools.Method(generatedUIPrefabCreator.GetType(), "CollectGeneratedPrefabDefinitions");
            method?.Invoke(generatedUIPrefabCreator, new[] { _generatedPrefabContext });
        }

        public void OverridePrefab(string prefabName, string variantName, object creatorDelegate)
        {
            if (GeneratedPrefabs?.Invoke(_generatedPrefabContext) is not { } generatedPrefabs)
                return;

            if (!generatedPrefabs.Contains(prefabName))
            {
                return;
                //generatedPrefabs.Add(prefabName, new Dictionary<string, CreateGeneratedWidget>());
            }
            if (!((IDictionary) generatedPrefabs[prefabName]).Contains(variantName))
            {
                //generatedPrefabs[prefabName].Add(variantName, creatorDelegate);
                return;
            }

            ((IDictionary) generatedPrefabs[prefabName])[variantName] = creatorDelegate;
        }
    }
}