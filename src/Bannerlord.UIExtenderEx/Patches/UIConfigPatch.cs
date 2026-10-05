using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections.Generic;
using System.Diagnostics;

using TaleWorlds.Engine.GauntletUI;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Synchronizes <see cref="UIConfig.DoNotUseGeneratedPrefabs"/> with <see cref="UIExtenderExSettings.DisableGeneratedPrefabs"/>.
/// <para>
/// TaleWorlds stores generated prefab configuration in memory only. Intercepting both the property setter and the
/// <c>ui.use_generated_prefabs</c> console command ensures modifications persist across game sessions via <c>SubModule.xml</c>.
/// </para>
/// </summary>
internal static class UIConfigPatch
{
    public static void Patch(Harmony harmony)
    {
        if (!harmony.TryPatch(
                AccessTools2.DeclaredPropertySetter("TaleWorlds.Engine.GauntletUI.UIConfig:DoNotUseGeneratedPrefabs"),
                postfix: AccessTools2.DeclaredMethod(typeof(UIConfigPatch), nameof(DoNotUseGeneratedPrefabsPostfix))))
        {
            Trace.TraceWarning("UIExtenderEx: failed to patch UIConfig.DoNotUseGeneratedPrefabs, changes to it will not be saved as DisableGeneratedPrefabs");
        }

        if (!harmony.TryPatch(
                AccessTools2.DeclaredMethod("TaleWorlds.Engine.GauntletUI.UIConfig:SetUsingGeneratedPrefabs"),
                postfix: AccessTools2.DeclaredMethod(typeof(UIConfigPatch), nameof(SetUsingGeneratedPrefabsPostfix))))
        {
            Trace.TraceWarning("UIExtenderEx: failed to patch UIConfig.SetUsingGeneratedPrefabs, ui.use_generated_prefabs may not be saved as DisableGeneratedPrefabs");
        }
    }

    private static void DoNotUseGeneratedPrefabsPostfix(bool value) => UIExtenderExSettings.Instance.DisableGeneratedPrefabs = value;

    // Only a command the game accepted: a malformed one leaves the property as it was
    private static void SetUsingGeneratedPrefabsPostfix(List<string> args)
    {
        if (args is { Count: 1 } && int.TryParse(args[0], out var value) && value is 0 or 1)
            UIExtenderExSettings.Instance.DisableGeneratedPrefabs = value == 0;
    }
}