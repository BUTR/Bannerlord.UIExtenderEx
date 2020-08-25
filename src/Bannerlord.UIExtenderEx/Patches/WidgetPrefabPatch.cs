using HarmonyLib;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Patches
{
    internal static class WidgetPrefabPatch
    {
        public static void Patch(Harmony harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(WidgetPrefab), nameof(WidgetPrefab.LoadFrom)),
                transpiler: new HarmonyMethod(AccessTools.Method(typeof(WidgetPrefabPatch), nameof(WidgetPrefab_LoadFrom_Transpiler))));
        }

        private static IEnumerable<CodeInstruction> WidgetPrefab_LoadFrom_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var instructionsList = instructions.ToList();

            var constructor = AccessTools.Constructor(typeof(WidgetPrefab));

            var locals = method.GetMethodBody()?.LocalVariables;
            var typeLocal = locals?.FirstOrDefault(x => x.LocalType == typeof(WidgetPrefab));

            if (typeLocal == null)
                return instructionsList.AsEnumerable();

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
                return instructionsList.AsEnumerable();

            // PrefabComponent.Load(path, xmlDocument);
            instructionsList.InsertRange(startIndex + 1, new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(() => ProcessMovie(null!, null!)))
            });
            return instructionsList.AsEnumerable();
        }
        private static void ProcessMovie(string path, XmlDocument document)
        {
            foreach (var runtime in UIExtender.GetAllRuntimes())
            {
                if (!runtime.PrefabComponent.Enabled)
                    continue;

                var movieName = Path.GetFileNameWithoutExtension(path);
                runtime.PrefabComponent.ProcessMovieIfNeeded(movieName, document);
            }
        }
    }
}