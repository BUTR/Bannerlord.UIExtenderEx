using System.Reflection;

using HarmonyLib;

namespace Bannerlord.UIExtenderEx.Extensions
{
    /// <summary>Extension class for working with Harmony.</summary>
    internal static class HarmonyExtensions
    {
        public static bool TryPatch(this Harmony harmony,
            MethodBase? original,
            MethodInfo? prefix = null,
            MethodInfo? postfix = null,
            MethodInfo? transpiler = null,
            MethodInfo? finalizer = null)
        {
            if (original is null || (prefix is null && postfix is null && transpiler is null && finalizer is null))
                return false;

            var prefixMethod = prefix is null ? null : new HarmonyMethod(prefix);
            var postfixMethod = postfix is null ? null : new HarmonyMethod(postfix);
            var transpilerMethod = transpiler is null ? null : new HarmonyMethod(transpiler);
            var finalizerMethod = finalizer is null ? null : new HarmonyMethod(finalizer);

            harmony.Patch(original, prefixMethod, postfixMethod, transpilerMethod, finalizerMethod);

            return true;
        }

        public static ReversePatcher? TryCreateReversePatcher(this Harmony harmony,
            MethodBase? original = null,
            MethodInfo? standin = null)
        {
            if (original is null || standin is null)
                return null;

            return harmony.CreateReversePatcher(original, new HarmonyMethod(standin));
        }
    }
}