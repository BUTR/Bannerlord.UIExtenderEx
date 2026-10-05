using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Prevents memory leaks and release stalls caused by duplicate widget templates accumulating in <c>_customTypeChildren</c>.
/// <para>
/// Vanilla <c>WidgetTemplate.CreateWidgets</c> repeatedly appends child templates to <c>_customTypeChildren</c> on each instantiation
/// without clearing or deduplicating, exponentially inflating <c>WidgetTemplate.OnRelease</c> traversal duration.
/// </para>
/// </summary>
public static class WidgetTemplatePatch
{
    public static void Patch(Harmony harmony)
    {
        if (!harmony.TryPatch(
                AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate:CreateWidgets"),
                transpiler: AccessTools2.DeclaredMethod(typeof(WidgetTemplatePatch), nameof(CreateWidgetsTranspiler))))
        {
            Trace.TraceWarning("UIExtenderEx: could not patch WidgetTemplate.CreateWidgets; screens whose lists are rebuilt often release slowly");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> CreateWidgetsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var list = instructions.ToList();
        var field = AccessTools2.DeclaredField("TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate:_customTypeChildren");
        var addRange = AccessTools2.DeclaredMethod(typeof(List<WidgetTemplate>), nameof(List<WidgetTemplate>.AddRange));
        var addDistinct = AccessTools2.DeclaredMethod(typeof(WidgetTemplatePatch), nameof(AddDistinct));
        if (field is null || addRange is null || addDistinct is null)
            return Unchanged(list, "a member it changes was not found");

        // Locate the AddRange call that populates _customTypeChildren.
        var calls = list.Select((x, i) => (Instruction: x, Index: i))
            .Where(x => x.Instruction.Calls(addRange))
            .ToList();
        if (calls.Count != 1 || !list.Take(calls[0].Index).Any(x => x.LoadsField(field)))
            return Unchanged(list, $"{calls.Count} calls of AddRange");

        // Replace List<WidgetTemplate>.AddRange with static AddDistinct call to deduplicate templates in place.
        var call = calls[0].Instruction;
        call.opcode = OpCodes.Call;
        call.operand = addDistinct;
        return list;
    }

    private static IEnumerable<CodeInstruction> Unchanged(List<CodeInstruction> instructions, string reason)
    {
        Trace.TraceWarning("UIExtenderEx: WidgetTemplate.CreateWidgets left as it is ({0}); screens whose lists are rebuilt often release slowly", reason);
        return instructions;
    }

    /// <summary>Adds unique templates from <paramref name="templates"/> to <paramref name="list"/>.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddDistinct(List<WidgetTemplate> list, IEnumerable<WidgetTemplate> templates)
    {
        foreach (var template in templates)
        {
            if (!list.Contains(template))
                list.Add(template);
        }
    }
}