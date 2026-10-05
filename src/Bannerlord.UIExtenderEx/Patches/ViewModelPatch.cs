using Bannerlord.UIExtenderEx.Utils;
using Bannerlord.UIExtenderEx.ViewModels;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Patches <c>ViewModel.ExecuteCommand</c> to forward command invocations on <see cref="ViewModelWrapper"/> instances
/// to the underlying wrapped ViewModel.
/// </summary>
internal static class ViewModelPatch
{
    public static void Patch(Harmony harmony)
    {
        if (!harmony.TryPatch(
                AccessTools2.Method("TaleWorlds.Library.ViewModel:ExecuteCommand"),
                prefix: AccessTools2.DeclaredMethod(typeof(ViewModelPatch), nameof(ExecuteCommandPatch))))
        {
            MessageUtils.DisplayUserWarning("Failed to patch ViewModel.ExecuteCommand! Buttons that mods add to screens may do nothing when clicked.");
        }
    }

    /// <summary>
    /// Forwards command execution to the wrapped ViewModel instance when invoked on a <see cref="ViewModelWrapper"/>.
    /// </summary>
    private static bool ExecuteCommandPatch(object __instance, string commandName, object[] parameters)
    {
        if (__instance is ViewModelWrapper { Object: { } viewModel })
        {
            viewModel.ExecuteCommand(commandName, parameters);
            return false;
        }

        return true;
    }
}