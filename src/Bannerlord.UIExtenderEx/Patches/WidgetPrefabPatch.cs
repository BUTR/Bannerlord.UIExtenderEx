using System;

using HarmonyLib;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Patches
{
    internal static class WidgetPrefabPatch
    {
        public static void Patch(Harmony harmony)
        {
            harmony.Patch(
                AccessTools.DeclaredMethod(typeof(WidgetPrefab), nameof(WidgetPrefab.LoadFrom)),
                transpiler: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(WidgetPrefabPatch), nameof(WidgetPrefab_LoadFrom_Transpiler))));

            harmony.CreateReversePatcher(
                SymbolExtensions.GetMethodInfo(() => WidgetPrefab.LoadFrom(null!, null!, null!)),
                new HarmonyMethod(SymbolExtensions.GetMethodInfo(() => LoadFromDocument(null!, null!, null!, null!)))).Patch();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<CodeInstruction> WidgetPrefab_LoadFrom_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var instructionsList = instructions.ToList();

            [MethodImpl(MethodImplOptions.NoInlining)]
            IEnumerable<CodeInstruction> ReturnDefault(string place)
            {
                Utils.DisplayUserWarning("Failed to patch WidgetPrefab.LoadFrom! {0}", place);
                return instructionsList.AsEnumerable();
            }

            var constructor = AccessTools.DeclaredConstructor(typeof(WidgetPrefab));
            var processMovieMethod = SymbolExtensions.GetMethodInfo(() => ProcessMovie(null!, null!));

            var locals = method.GetMethodBody()?.LocalVariables;
            var typeLocal = locals?.FirstOrDefault(x => x.LocalType == typeof(WidgetPrefab));

            if (typeLocal is null)
            {
                return ReturnDefault("Local not found");
            }

            var startIndex = -1;
            for (var i = 0; i < instructionsList.Count - 2; i++)
            {
                if (instructionsList[i + 0].opcode != OpCodes.Newobj || !Equals(instructionsList[i + 0].operand, constructor))
                    continue;

                if (!instructionsList[i + 1].IsStloc())
                    continue;

                startIndex = i;
                break;
            }

            if (startIndex == -1)
            {
                return ReturnDefault("Pattern not found");
            }

            // ProcessMovie(path, xmlDocument);
            instructionsList.InsertRange(startIndex + 1, new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldloc_0),
                new(OpCodes.Call, processMovieMethod)
            });
            return instructionsList.AsEnumerable();
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ProcessMovie(string path, XmlDocument document)
        {
            foreach (var runtime in UIExtender.GetAllRuntimes())
            {
                if (!runtime.PrefabComponent.Enabled)
                {
                    continue;
                }

                var movieName = Path.GetFileNameWithoutExtension(path);
                runtime.PrefabComponent.ProcessMovieIfNeeded(movieName, document);
            }
        }

        // We can call a slightly modified native game call this way
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static WidgetPrefab LoadFromDocument(PrefabExtensionContext prefabExtensionContext, WidgetAttributeContext widgetAttributeContext, string path, XmlDocument document)
        {
            // Replaces reading XML from file with assigning it from the new local variable `XmlDocument document`
            [MethodImpl(MethodImplOptions.NoInlining)]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
            {
                var returnNull = new List<CodeInstruction>
                {
                    new (OpCodes.Ldnull),
                    new (OpCodes.Ret)
                }.AsEnumerable();

                var instructionList = instructions.ToList();

                var locals = method.GetMethodBody()?.LocalVariables;
                var typeLocal = locals?.FirstOrDefault(x => x.LocalType == typeof(XmlDocument));

                if (typeLocal is null)
                {
                    return returnNull;
                }

                var constructorIndex = -1;
                var constructor = AccessTools.Constructor(typeof(WidgetPrefab));
                for (var i = 0; i < instructionList.Count; i++)
                {
                    if (instructionList[i].opcode == OpCodes.Newobj && Equals(instructionList[i].operand, constructor))
                        constructorIndex = i;
                }

                if (constructorIndex == -1)
                {
                    return returnNull;
                }

                for (var i = 0; i < constructorIndex; i++)
                {
                    instructionList[i] = new CodeInstruction(OpCodes.Nop);
                }

                instructionList[constructorIndex - 2] = new CodeInstruction(OpCodes.Ldarg_S, 3);
                instructionList[constructorIndex - 1] = new CodeInstruction(OpCodes.Stloc_S, typeLocal.LocalIndex);

                return instructionList.AsEnumerable();
            }

            // make compiler happy
            _ = Transpiler(null!, null!);

            // make analyzer happy
            prefabExtensionContext.AddExtension(null);
            widgetAttributeContext.RegisterKeyType(null);
            path.Do(null);
            document.Validate(null);

            // make compiler happy
            return null!;
        }
    }
}