using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Patches
{
    internal static partial class WidgetCodeGenerationInfoDatabindingExtensionPatch
    {
        private delegate PropertyInfo? GetPropertyDelegate(Type type, string name);
        private static readonly GetPropertyDelegate? GetProperty;

        private delegate Type GetViewModelTypeAtPathDelegate(Type type, BindingPath path);
        private static readonly GetViewModelTypeAtPathDelegate? GetViewModelTypeAtPath;

        private delegate Type GetChildTypeAtPathDelegate(Type bindingListType, BindingPath path);
        private static readonly GetChildTypeAtPathDelegate? GetChildTypeAtPath;

        static WidgetCodeGenerationInfoDatabindingExtensionPatch()
        {
            GetProperty = AccessTools2.GetDelegate<GetPropertyDelegate>(
                AccessTools.Method("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.WidgetCodeGenerationInfoDatabindingExtension:GetProperty"));

            GetViewModelTypeAtPath = AccessTools2.GetDelegate<GetViewModelTypeAtPathDelegate>(
                AccessTools.Method("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.WidgetCodeGenerationInfoDatabindingExtension:GetViewModelTypeAtPath"));

            GetChildTypeAtPath = AccessTools2.GetDelegateObjectInstance<GetChildTypeAtPathDelegate>(
                AccessTools.Method("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.WidgetCodeGenerationInfoDatabindingExtension:GetChildTypeAtPath"));
        }

        public static void Patch(Harmony harmony)
        {
            if (AccessTools.Method("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.WidgetCodeGenerationInfoDatabindingExtension:GetViewModelTypeAtPath") is { } methodInfo)
            {
                harmony.Patch(
                    methodInfo,
                    transpiler: new HarmonyMethod(AccessTools.Method(typeof(WidgetCodeGenerationInfoDatabindingExtensionPatch), nameof(GetViewModelTypeAtPathTranspiler))));
            }
        }
    }
}