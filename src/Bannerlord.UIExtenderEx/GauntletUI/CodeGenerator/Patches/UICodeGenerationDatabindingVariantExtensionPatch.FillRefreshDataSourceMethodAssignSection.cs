using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Patches
{
    internal static partial class UICodeGenerationDatabindingVariantExtensionPatch
    {
        private static IEnumerable<CodeInstruction> FillRefreshDataSourceMethodAssignSectionReturnDefault(IEnumerable<CodeInstruction> instructions, string place)
        {
            Utils.DisplayUserWarning("Failed to patch UICodeGenerationDatabindingVariantExtension.FillRefreshDataSourceMethodAssignSection! {0}", place);
            return instructions;
        }

        private static IEnumerable<CodeInstruction> FillRefreshDataSourceMethodAssignSectionTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator, MethodBase method)
        {
            return FillRefreshDataSourceMethodAssignSectionUseVMMixinTranspiler(instructions);
        }

        private static IEnumerable<CodeInstruction> FillRefreshDataSourceMethodAssignSectionUseVMMixinTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var instructionsList = instructions.ToList();

            var methodCodeType = AccessTools.TypeByName("TaleWorlds.Library.CodeGeneration.MethodCode");

            var startIndex = -1;
            for (var i = 0; i < instructionsList.Count - 4; i++)
            {
                if (!instructionsList[i + 0].IsLdloc() && instructionsList[i + 0].operand is LocalBuilder lb && lb.LocalType != methodCodeType)
                    continue;

                //if (instructionsList[i + 1].opcode != OpCodes.Ldc_I4_S)
                //    continue;

                if (instructionsList[i + 2].opcode != OpCodes.Newarr)
                    continue;

                startIndex = i;
                break;
            }

            if (startIndex == -1)
                return FillRefreshDataSourceMethodAssignSectionReturnDefault(instructionsList, "UseVMMixin - Start Pattern not found");

            var count = IndexOf(instructionsList.Skip(startIndex), i => i.Calls(AccessTools.Method("TaleWorlds.Library.CodeGeneration.MethodCode:AddLine")));
            if (count == -1)
                return FillRefreshDataSourceMethodAssignSectionReturnDefault(instructionsList,"UseVMMixin - End Pattern not found");

            var endIndex = startIndex + count;

            var startIndexLabels = instructionsList[startIndex].labels;
            var endIndexLabels = instructionsList[endIndex].labels;
            var newInstructions = new[]
            {
                // load this
                new CodeInstruction(OpCodes.Ldarg_0) { labels = startIndexLabels },
                // load this._dataSourceType as arg0
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.UICodeGenerationDatabindingVariantExtension"), "_dataSourceType")),
                // load bindingPath as arg1
                new CodeInstruction(OpCodes.Ldloc_0),
                // methodCode as arg2
                new CodeInstruction(OpCodes.Ldarg_2),
                // Call FillRefreshDataSourceMethodAssignSectionMixin
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UICodeGenerationDatabindingVariantExtensionPatch), nameof(FillRefreshDataSourceMethodAssignSectionMixin)))
                {
                    labels = endIndexLabels
                }
            };

            instructionsList.RemoveRange(startIndex, endIndex - startIndex + 1);
            instructionsList.InsertRange(startIndex, newInstructions);

            return instructionsList;
        }

        private static void FillRefreshDataSourceMethodAssignSectionMixin(Type dataSourceType, BindingPath bindingPath, object methodCode)
        {
            if (GetDataSourceVariableNameOfPath is null || GetGenericTypeCodeFileName is null || AddLine is null || GetViewModelTypeAtPath is null)
                return;

            var parentDataSourceVariableNameOfPath = GetDataSourceVariableNameOfPath(bindingPath.ParentPath);
            var dataSourceVariableNameOfPath = GetDataSourceVariableNameOfPath(bindingPath);
            var lastNode = bindingPath.LastNode;

            if (!PropertyExists(dataSourceType, lastNode))
            {
                var viewModelTypeAtPath = GetViewModelTypeAtPath(dataSourceType, bindingPath);
                AddLine(methodCode, $"{dataSourceVariableNameOfPath} = ({GetGenericTypeCodeFileName(viewModelTypeAtPath)}){parentDataSourceVariableNameOfPath}.GetPropertyValue(\"{lastNode}\");");
            }
            else
            {
                AddLine(methodCode, $"{dataSourceVariableNameOfPath} = {parentDataSourceVariableNameOfPath}.{lastNode};");
            }
        }
    }
}