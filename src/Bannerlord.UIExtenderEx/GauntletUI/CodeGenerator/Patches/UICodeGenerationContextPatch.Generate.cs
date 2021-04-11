using HarmonyLib;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Patches
{
    internal static partial class UICodeGenerationContextPatch
    {
        private static IEnumerable<CodeInstruction> GenerateReturnDefault(IEnumerable<CodeInstruction> instructions, string place)
        {
            Utils.DisplayUserWarning("Failed to patch UICodeGenerationContext.Generate! {0}", place);
            return instructions;
        }

        private static IEnumerable<CodeInstruction> GenerateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator, MethodBase method)
        {
            return GenerateReplacePathTranspiler(instructions);
        }

        private static IEnumerable<CodeInstruction> GenerateReplacePathTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();

            var startIndex = -1;
            for (var i = 0; i < instructionsList.Count - 1; i++)
            {
                if (!instructionsList[i + 0].Calls(AccessTools.Method(typeof(Directory), nameof(Directory.GetCurrentDirectory))))
                    continue;

                if (instructionsList[i + 1].opcode != OpCodes.Ldstr)
                    continue;

                startIndex = i;
                break;
            }
            if (startIndex == -1)
                return GenerateReturnDefault(instructionsList, "ReplacePath - Start Pattern not found");

            var endIndex = -1;
            for (var i = startIndex; i < instructionsList.Count - 1; i++)
            {
                if (!instructionsList[i + 0].Calls(AccessTools.Method(typeof(Path), nameof(Path.GetFullPath))))
                    continue;

                if (!instructionsList[i + 1].IsStloc())
                    continue;

                endIndex = i + 1;
                break;
            }
            if (endIndex == -1)
                return GenerateReturnDefault(instructionsList, "ReplacePath - End Pattern not found");

            var startIndexLabels = instructionsList[startIndex].labels;
            var endIndexLabels = instructionsList[endIndex].labels;
            var newInstructions = new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0) { labels = startIndexLabels },
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.UICodeGenerationContext"), "_outputFolder")),
                new CodeInstruction(OpCodes.Stloc_1) { labels = endIndexLabels },
            };

            instructionsList.RemoveRange(startIndex, endIndex - startIndex + 1);
            instructionsList.InsertRange(startIndex, newInstructions);

            return instructionsList;
        }
    }
}