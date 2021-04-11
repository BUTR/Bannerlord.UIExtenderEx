using HarmonyLib;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Patches
{
    internal static partial class UICodeGenerationDatabindingVariantExtensionPatch
    {
        private static IEnumerable<CodeInstruction> CreateViewModelPropertyChangedMethodsReturnDefault(IEnumerable<CodeInstruction> instructions, string place)
        {
            Utils.DisplayUserWarning("Failed to patch UICodeGenerationDatabindingVariantExtension.CreateViewModelPropertyChangedMethods! {0}", place);
            return instructions;
        }

        private static IEnumerable<CodeInstruction> CreateViewModelPropertyChangedMethodsTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator, MethodBase method)
        {
            return CreateViewModelPropertyChangedMethodsUseVMMixinTranspiler(instructions, method);
        }

        private static IEnumerable<CodeInstruction> CreateViewModelPropertyChangedMethodsUseVMMixinTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var instructionsList = instructions.ToList();

            var methodCodeType = AccessTools.TypeByName("TaleWorlds.Library.CodeGeneration.MethodCode");
            var locals = method.GetMethodBody()?.LocalVariables;
            var typeLocal = locals?.FirstOrDefault(x => x.LocalType == methodCodeType);

            if (typeLocal is null)
                return CreateViewModelPropertyChangedMethodsReturnDefault(instructionsList, "UseVMMixin - Local not found");

            var startIndex = -1;
            for (var i = 0; i < instructionsList.Count - 2; i++)
            {
                if (!instructionsList[i + 0].IsLdloc() || instructionsList[i + 0].operand is LocalBuilder lb && lb.LocalType != methodCodeType)
                    continue;

                //if (instructionsList[i + 1].opcode != OpCodes.Ldc_I4_S)
                //    continue;

                if (instructionsList[i + 2].opcode != OpCodes.Newarr)
                    continue;

                startIndex = i;
                break;
            }

            if (startIndex == -1)
                return CreateViewModelPropertyChangedMethodsReturnDefault(instructionsList, "UseVMMixin - Start Pattern not found");

            var count = IndexOf(instructionsList.Skip(startIndex), i => i.Calls(AccessTools.Method("TaleWorlds.Library.CodeGeneration.MethodCode:AddLine")));
            if (count == -1)
                return CreateViewModelPropertyChangedMethodsReturnDefault(instructionsList, "UseVMMixin - End Pattern not found");

            var endIndex = startIndex + count;

            var startIndexLabels = instructionsList[startIndex].labels;
            var endIndexLabels = instructionsList[endIndex].labels;
            var newInstructions = new[]
            {
                // load this
                new CodeInstruction(OpCodes.Ldarg_0) { labels = startIndexLabels },
                // load this._dataSourceType as arg0
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.UICodeGenerationDatabindingVariantExtension"), "_dataSourceType")),

                // load reference to IEnumerable<BindingPathTargetDetails>
                new CodeInstruction(OpCodes.Ldloca_S, 9),
                // Call IEnumerable<BindingPathTargetDetails>.get_Current
                // load return value as arg1
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(List<>.Enumerator).MakeGenericType(AccessTools.TypeByName("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.BindingPathTargetDetails")), nameof(IEnumerator.Current))),

                // load methodCode as arg2
                new CodeInstruction(OpCodes.Ldloc_S, 7),
                // Call CreateViewModelPropertyChangedMethodsMixin
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UICodeGenerationDatabindingVariantExtensionPatch), nameof(CreateViewModelPropertyChangedMethodsMixin)))
                {
                    labels = endIndexLabels
                }
            };

            instructionsList.RemoveRange(startIndex, endIndex - startIndex + 1);
            instructionsList.InsertRange(startIndex, newInstructions);

            return instructionsList;
        }

        private static void CreateViewModelPropertyChangedMethodsMixin(Type dataSourceType, object bindingPathTargetDetails, object methodCode)
        {
            if (GetDataSourceVariableNameOfPath is null || GetGenericTypeCodeFileName is null || AddLine is null || GetViewModelTypeAtPath is null)
                return;

            var child = BindingPathTargetDetailsHandle.FromExisting(bindingPathTargetDetails);
            var parent = child.Parent.GetValueOrDefault();

            var bindingPath = child.BindingPath;

            var parentDataSourceVariableNameOfPath = GetDataSourceVariableNameOfPath(parent.BindingPath);
            var dataSourceVariableNameOfPath = GetDataSourceVariableNameOfPath(bindingPath);
            var lastNode = bindingPath.LastNode;

            if (!PropertyExists(dataSourceType, lastNode))
            {
                var childViewModelTypeAtPath = GetViewModelTypeAtPath(dataSourceType, bindingPath);
                AddLine(methodCode, $"RefreshDataSource{dataSourceVariableNameOfPath}(({GetGenericTypeCodeFileName(childViewModelTypeAtPath)}){parentDataSourceVariableNameOfPath}.GetPropertyValue(\"{lastNode}\"));");
            }
            else
            {
                AddLine(methodCode, $"RefreshDataSource{dataSourceVariableNameOfPath}({parentDataSourceVariableNameOfPath}.{lastNode});");
            }
        }
    }
}