using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Patches
{
    /// <summary>
    /// Skips type duplicates
    /// </summary>
    public static class WidgetFactoryPatch
    {
        private static bool TranspilerSuccessful = false;

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(
                SymbolExtensions.GetMethodInfo((WidgetFactory wf) => wf.Initialize()),
                transpiler: new HarmonyMethod(SymbolExtensions.GetMethodInfo(() => InitializeTranspiler(null!, null!))));

            // Transpilers are very sensitive to code changes.
            // We can fall back to the old implementation of Initialize() as a last effort.
            if (!TranspilerSuccessful)
            {
                harmony.Patch(
                    SymbolExtensions.GetMethodInfo((WidgetFactory wf) => wf.Initialize()),
                    prefix: new HarmonyMethod(SymbolExtensions.GetMethodInfo(() => InitializePrefix(null!))));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<CodeInstruction> InitializeTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var instructionsList = instructions.ToList();

            IEnumerable<CodeInstruction> ReturnDefault(string place)
            {
                Utils.DisplayUserWarning("Failed to patch WidgetPrefab.LoadFrom! {0}", place);
                return instructionsList.AsEnumerable();
            }

            var locals = method.GetMethodBody()?.LocalVariables;
            var typeLocal = locals?.FirstOrDefault(x => x.LocalType == typeof(Type));

            if (typeLocal == null)
                return ReturnDefault("Local not found");

            var startIndex = -1;
            var endIndex = -1;
            for (var i = 0; i < instructionsList.Count - 5; i++)
            {
                if (!instructionsList[i + 0].IsLdarg(0))
                    continue;

                if (!instructionsList[i + 1].LoadsField(AccessTools.DeclaredField(typeof(WidgetFactory), "_builtinTypes")))
                    continue;

                if (!instructionsList[i + 2].IsLdloc())
                       continue;

                if (!instructionsList[i + 3].Calls(AccessTools.DeclaredPropertyGetter(typeof(MemberInfo), nameof(MemberInfo.Name))))
                    continue;

                if (!instructionsList[i + 4].IsLdloc())
                    continue;

                if (!instructionsList[i + 5].Calls(SymbolExtensions.GetMethodInfo((Dictionary<string, Type> d) => d.Add(null!, null!))))
                    continue;

                startIndex = i;
                endIndex = i + 5;
                break;
            }

            if (startIndex == -1)
                return ReturnDefault("Pattern not found");

            if (instructionsList[endIndex + 1].labels.Count == 0)
                return ReturnDefault("Jmp was not found");

            var jmpEnumerator = instructionsList[endIndex + 1].labels.FirstOrDefault();

            // if (!this._builtinTypes.ContainsKey(type.Name))
            instructionsList.InsertRange(startIndex, new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(WidgetFactory), "_builtinTypes")),
                new CodeInstruction(OpCodes.Ldloc, typeLocal.LocalIndex),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.DeclaredPropertyGetter(typeof(MemberInfo), nameof(MemberInfo.Name))),
                new CodeInstruction(OpCodes.Callvirt, SymbolExtensions.GetMethodInfo((Dictionary<string, Type> d) => d.ContainsKey(null!))),
                new CodeInstruction(OpCodes.Brtrue_S, jmpEnumerator)
            });
            TranspilerSuccessful = true;
            return instructionsList.AsEnumerable();
        }

        private static AccessTools.FieldRef<WidgetFactory, Dictionary<string, Type>> BuiltinTypesField { get; } =
            AccessTools.FieldRefAccess<WidgetFactory, Dictionary<string, Type>>("_builtinTypes");
        private static MethodInfo GetPrefabNamesAndPathsFromCurrentPathMethod { get; } =
            AccessTools.DeclaredMethod(typeof(WidgetFactory), "GetPrefabNamesAndPathsFromCurrentPath");

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool InitializePrefix(WidgetFactory __instance)
        {
            var builtinTypes = BuiltinTypesField(__instance);
            if (builtinTypes == null || !(GetPrefabNamesAndPathsFromCurrentPathMethod.Invoke(__instance, Array.Empty<object>()) is Dictionary<string, string> prefabsData))
                return true; // fallback

            foreach (var prefabExtension in __instance.PrefabExtensionContext.PrefabExtensions)
            {
                var method = AccessTools.Method(prefabExtension.GetType(), "RegisterAttributeTypes");
                method.Invoke(prefabExtension, new object[] { __instance.WidgetAttributeContext });
            }
            foreach (var type in WidgetInfo.CollectWidgetTypes())
            {
                // PATCH
                if (!builtinTypes.ContainsKey(type.Name))
                // PATCH
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