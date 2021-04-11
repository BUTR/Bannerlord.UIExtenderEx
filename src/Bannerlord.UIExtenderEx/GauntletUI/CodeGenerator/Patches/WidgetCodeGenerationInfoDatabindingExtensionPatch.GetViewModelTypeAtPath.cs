using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Patches
{
    internal static partial class WidgetCodeGenerationInfoDatabindingExtensionPatch
    {
        private static IEnumerable<CodeInstruction> GetViewModelTypeAtPathTranspilerReturnDefault(IEnumerable<CodeInstruction> instructions, string place)
        {
            Utils.DisplayUserWarning("Failed to patch WidgetCodeGenerationInfoDatabindingExtension.GetViewModelTypeAtPath! {0}", place);
            return instructions;
        }

        private static IEnumerable<CodeInstruction> GetViewModelTypeAtPathTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator, MethodBase method)
        {
            return GetViewModelTypeAtPathUseVMMixinTranspiler(instructions);
        }

        private static IEnumerable<CodeInstruction> GetViewModelTypeAtPathUseVMMixinTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();

            var startIndex = -1;
            var endIndex = -1;
            for (var i = 0; i < instructionsList.Count - 1; i++)
            {
                if (instructionsList[i + 0].opcode != OpCodes.Ldnull)
                    continue;

                if (instructionsList[i + 1].opcode != OpCodes.Ret)
                    continue;

                startIndex = i;
                endIndex = i + 1;
                break;
            }

            if (startIndex == -1)
                return GetViewModelTypeAtPathTranspilerReturnDefault(instructionsList, "UseVMMixin - Pattern not found");

            var startIndexLabels = instructionsList[startIndex].labels;
            var endIndexLabels = instructionsList[endIndex].labels;
            var newInstructions = new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0) { labels = startIndexLabels },
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WidgetCodeGenerationInfoDatabindingExtensionPatch), nameof(GetViewModelMixinTypeAtPath))),
                new CodeInstruction(OpCodes.Ret) { labels = endIndexLabels }
            };

            instructionsList.RemoveRange(startIndex, endIndex - startIndex + 1);
            instructionsList.InsertRange(startIndex, newInstructions);

            return instructionsList;
        }


        private static Type? GetViewModelMixinTypeAtPath(Type type, BindingPath path)
        {
            if (GetProperty is null || GetViewModelTypeAtPath is null || GetChildTypeAtPath is null)
                return null;

            var mixins = UIExtender.GetAllRuntimes()
                .Where(r => r.ViewModelComponent.Enabled)
                .SelectMany(r => r.ViewModelComponent.Mixins.TryGetValue(type, out var list) ? list : Enumerable.Empty<Type>())
                .ToArray();

            var subPath = path.SubPath;

            foreach (var mixin in mixins)
            {
                var mixinProperty = GetProperty(mixin, subPath.FirstNode);
                if (mixinProperty is not null)
                {
                    var returnType = mixinProperty.GetGetMethod().ReturnType;
                    if (typeof(ViewModel).IsAssignableFrom(returnType))
                    {
                        return GetViewModelTypeAtPath(returnType, subPath);
                    }
                    if (typeof(IMBBindingList).IsAssignableFrom(returnType))
                    {
                        return GetChildTypeAtPath(returnType, subPath);
                    }
                }
            }

            return null;
        }
    }
}