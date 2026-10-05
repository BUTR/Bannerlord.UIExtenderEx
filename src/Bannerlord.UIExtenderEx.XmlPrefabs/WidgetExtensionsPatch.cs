using Bannerlord.UIExtenderEx.Runtimes;
using Bannerlord.UIExtenderEx.XmlPrefabs;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Fixes Gauntlet XML attribute path resolution for dotted property paths with three or more segments.
/// <para>
/// In vanilla Gauntlet, <c>WidgetExtensions.GetObjectAndProperty</c> calculates segment substrings using an absolute
/// separator index instead of the segment length, causing property resolution to truncate or throw exceptions on nested properties.
/// </para>
/// </summary>
public static class WidgetExtensionsPatch
{
    /// <summary>
    /// Gets a value indicating whether multi-segment dotted attribute paths resolve correctly in the XML loader.
    /// Synchronized with <see cref="PrefabRuntimes.DottedAttributePathsResolve"/>.
    /// </summary>
    public static bool DottedPathsResolveCorrectly
    {
        get => PrefabRuntimes.DottedAttributePathsResolve;
        private set => PrefabRuntimes.DottedAttributePathsResolve = value;
    }

    public static void Patch(Harmony harmony)
    {
        if (AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions:GetObjectAndProperty") is not { } getObjectAndProperty)
        {
            MessageUtils.DisplayUserWarning("Failed to patch WidgetExtensions.GetObjectAndProperty (method not found)! Some screens may ignore parts of their layout, such as text sizes, and look different than intended.");
            return;
        }

        // Harmony executes the transpiler during patch creation; the state flag settles immediately.
        harmony.TryPatch(
            getObjectAndProperty,
            transpiler: AccessTools2.DeclaredMethod(typeof(WidgetExtensionsPatch), nameof(GetObjectAndProperty_Transpiler)));

        if (!DottedPathsResolveCorrectly)
            MessageUtils.DisplayUserWarning("Failed to patch WidgetExtensions.GetObjectAndProperty! Some screens may ignore parts of their layout, such as text sizes, and look different than intended.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> GetObjectAndProperty_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var instructionsList = instructions.ToList();
        // Reset state before re-evaluating instructions in case another Harmony patch triggers recompilation.
        DottedPathsResolveCorrectly = false;

        // Match string.Substring(int startIndex, int length).
        var substring = AccessTools2.DeclaredMethod(typeof(string), nameof(string.Substring), [typeof(int), typeof(int)]);
        if (substring is null)
            return instructionsList.AsEnumerable();

        var found = 0;
        for (var i = 0; i < instructionsList.Count; i++)
        {
            if (!instructionsList[i].Calls(substring))
                continue;

            // Calculate segment length by subtracting nameStartIndex (arg 2) from separatorIndex on the stack.
            instructionsList.Insert(i, new(OpCodes.Ldarg_2));
            instructionsList.Insert(i + 1, new(OpCodes.Sub));
            i += 2;
            found++;
        }

        if (found != 1)
        {
            MessageUtils.DisplayUserWarning("Failed to patch WidgetExtensions.GetObjectAndProperty (expected one Substring(int, int) call, found {0})! Some screens may ignore parts of their layout, such as text sizes, and look different than intended.", found);
            return instructions;
        }

        DottedPathsResolveCorrectly = true;
        return instructionsList.AsEnumerable();
    }
}