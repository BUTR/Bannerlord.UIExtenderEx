using Bannerlord.UIExtenderEx.Utils;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Patches;

internal static class ParsePatch
{
    public static void Patch(Harmony harmony)
    {
        if (AccessTools2.DeclaredMethod(typeof(ConstantDefinition), nameof(ConstantDefinition.GetValue)) is { } getValueMethod)
        {
            harmony.Patch(
                getValueMethod,
                transpiler: new HarmonyMethod(typeof(ParsePatch), nameof(ConstantDefinition_GetValue_Transpiler)));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> ConstantDefinition_GetValue_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var instructionsList = instructions.ToList();

        [MethodImpl(MethodImplOptions.NoInlining)]
        IEnumerable<CodeInstruction> ReturnDefault(string place)
        {
            MessageUtils.DisplayUserWarning("Failed to patch ConstantDefinition.GetValue! {0}", place);
            return instructionsList.AsEnumerable();
        }

        var decimalParseString = AccessTools2.DeclaredMethod(typeof(decimal), nameof(decimal.Parse), [typeof(string)]);
        if (decimalParseString is null)
            return ReturnDefault("decimal.Parse(string) method not found");

        var decimalParseStringProvider = AccessTools2.DeclaredMethod(typeof(decimal), nameof(decimal.Parse), [typeof(string), typeof(IFormatProvider)]);
        if (decimalParseStringProvider is null)
            return ReturnDefault("decimal.Parse(string, IFormatProvider) method not found");

        var invariantCultureGetter = AccessTools2.DeclaredPropertyGetter(typeof(CultureInfo), nameof(CultureInfo.InvariantCulture));
        if (invariantCultureGetter is null)
            return ReturnDefault("CultureInfo.InvariantCulture getter not found");

        var found = false;
        for (var i = 0; i < instructionsList.Count; i++)
        {
            if (instructionsList[i].opcode == OpCodes.Call && Equals(instructionsList[i].operand, decimalParseString))
            {
                instructionsList.Insert(i, new CodeInstruction(OpCodes.Call, invariantCultureGetter));
                i++;
                instructionsList[i] = new CodeInstruction(OpCodes.Call, decimalParseStringProvider);
                found = true;
            }
        }

        if (!found)
            return ReturnDefault("decimal.Parse(string) call not found in method");

        return instructionsList.AsEnumerable();
    }
}