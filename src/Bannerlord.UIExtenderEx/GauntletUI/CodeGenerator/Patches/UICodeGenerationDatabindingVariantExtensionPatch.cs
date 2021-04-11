using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Patches
{
    internal static partial class UICodeGenerationDatabindingVariantExtensionPatch
    {
        static int IndexOf<T>(IEnumerable<T> source, Func<T, bool> predicate)
        {
            var index = 0;
            foreach (var item in source)
            {
                if (predicate(item)) return index;
                index++;
            }
            return -1;
        }

        private delegate string GetDataSourceVariableNameOfPathDelegate(BindingPath bindingPath);
        private static readonly GetDataSourceVariableNameOfPathDelegate? GetDataSourceVariableNameOfPath;

        private delegate string GetGenericTypeCodeFileNameDelegate(Type type);
        private static readonly GetGenericTypeCodeFileNameDelegate? GetGenericTypeCodeFileName;

        private delegate Type GetViewModelTypeAtPathDelegate(Type bindingListType, BindingPath path);
        private static readonly GetViewModelTypeAtPathDelegate? GetViewModelTypeAtPath;

        private delegate void AddLineDelegate(object instance, string line);
        private static readonly AddLineDelegate? AddLine;

        static UICodeGenerationDatabindingVariantExtensionPatch()
        {
            GetDataSourceVariableNameOfPath = AccessTools2.GetDelegate<GetDataSourceVariableNameOfPathDelegate>(
                AccessTools.Method("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.UICodeGenerationDatabindingVariantExtension:GetDatasourceVariableNameOfPath"));

            GetGenericTypeCodeFileName = AccessTools2.GetDelegate<GetGenericTypeCodeFileNameDelegate>(
                AccessTools.Method("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.UICodeGenerationDatabindingVariantExtension:GetGenericTypeCodeFileName"));

            GetViewModelTypeAtPath = AccessTools2.GetDelegate<GetViewModelTypeAtPathDelegate>(
                AccessTools.Method("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.WidgetCodeGenerationInfoDatabindingExtension:GetViewModelTypeAtPath"));

            AddLine = AccessTools2.GetDelegateObjectInstance<AddLineDelegate>(
                AccessTools.Method("TaleWorlds.Library.CodeGeneration.MethodCode:AddLine"));
        }

        public static void Patch(Harmony harmony)
        {
            if (AccessTools.Method("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.UICodeGenerationDatabindingVariantExtension:FillRefreshDataSourceMethodAssignSection") is { } methodInfo1)
            {
                harmony.Patch(
                    methodInfo1,
                    transpiler: new HarmonyMethod(AccessTools.Method(typeof(UICodeGenerationDatabindingVariantExtensionPatch), nameof(FillRefreshDataSourceMethodAssignSectionTranspiler))));
            }

            if (AccessTools.Method("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.UICodeGenerationDatabindingVariantExtension:CreateViewModelPropertyChangedMethods") is { } methodInfo2)
            {
                harmony.Patch(
                    methodInfo2,
                    transpiler: new HarmonyMethod(AccessTools.Method(typeof(UICodeGenerationDatabindingVariantExtensionPatch), nameof(CreateViewModelPropertyChangedMethodsTranspiler))));
            }
        }

        private static bool PropertyExists(Type type, string name) => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(propertyInfo => propertyInfo.Name == name);
    }
}