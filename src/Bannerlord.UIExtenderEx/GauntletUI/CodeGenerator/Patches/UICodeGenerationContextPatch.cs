using HarmonyLib;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Patches
{
    internal static partial class UICodeGenerationContextPatch
    {
        public static void Patch(Harmony harmony)
        {
            if (AccessTools.Method("TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.UICodeGenerationContext:Generate") is { } methodInfo)
            {
                harmony.Patch(
                    methodInfo,
                    transpiler: new HarmonyMethod(AccessTools.Method(typeof(UICodeGenerationContextPatch), nameof(GenerateTranspiler))));
            }
        }
    }
}