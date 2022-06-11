using Bannerlord.BUTR.Shared.Extensions;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        private static readonly MethodInfo? _initializeMethod =
            AccessTools2.Method("TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:Initialize");

        private static bool _transpilerSuccessful;

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(
                _initializeMethod,
                transpiler: new HarmonyMethod(SymbolExtensions.GetMethodInfo(() => InitializeTranspiler(null!, null!))));

            // Transpilers are very sensitive to code changes.
            // We can fall back to the old implementation of Initialize() as a last effort.
            if (!_transpilerSuccessful)
            {
                harmony.Patch(
                    _initializeMethod,
                    prefix: new HarmonyMethod(SymbolExtensions.GetMethodInfo(() => InitializePrefix(null!))));
            }
        }

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
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

            if (typeLocal is null)
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
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(WidgetFactory), "_builtinTypes")),
                new(OpCodes.Ldloc, typeLocal.LocalIndex),
                new(OpCodes.Callvirt, AccessTools.DeclaredPropertyGetter(typeof(MemberInfo), nameof(MemberInfo.Name))),
                new(OpCodes.Callvirt, SymbolExtensions.GetMethodInfo((Dictionary<string, Type> d) => d.ContainsKey(null!))),
                new(OpCodes.Brtrue_S, jmpEnumerator)
            });
            _transpilerSuccessful = true;
            return instructionsList.AsEnumerable();
        }

        private static AccessTools.FieldRef<WidgetFactory, Dictionary<string, Type>>? BuiltinTypesField { get; } = AccessTools2.FieldRefAccess<WidgetFactory, Dictionary<string, Type>>("_builtinTypes");
        private static MethodInfo GetPrefabNamesAndPathsFromCurrentPathMethod { get; } = AccessTools.DeclaredMethod(typeof(WidgetFactory), "GetPrefabNamesAndPathsFromCurrentPath");

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool InitializePrefix(WidgetFactory __instance)
        {
            var builtinTypes = BuiltinTypesField is not null ? BuiltinTypesField(__instance) : null;
            if (builtinTypes is not null && GetPrefabNamesAndPathsFromCurrentPathMethod.Invoke(__instance, Array.Empty<object>()) is Dictionary<string, string> prefabsData)
            {
                foreach (var prefabExtension in __instance.PrefabExtensionContext.PrefabExtensions)
                {
                    var method = AccessTools.Method(prefabExtension.GetType(), "RegisterAttributeTypes");
                    method.Invoke(prefabExtension, new object[] { __instance.WidgetAttributeContext });
                }

                foreach (var type in WidgetInfo.CollectWidgetTypes())
                {
                    // PATCH
                    if (!builtinTypes.ContainsKey(type.Name))
                    {
                        // PATCH
                        builtinTypes.Add(type.Name, type);
                    }
                }

                foreach (var (key, value) in prefabsData)
                {
                    __instance.AddCustomType(key, value);
                }

                return false;
            }
            else
            {
                return true; // fallback
            }
        }
    }
}