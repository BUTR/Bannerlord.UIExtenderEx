using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.UIExtenderEx.Patches;

/// <summary>
/// Fixes a memory and per-frame update leak in <c>EventManager</c>, where widgets stay in the visual definition update
/// container after they no longer belong there.
/// <para>
/// From v1.3.12, <c>EventManager.UnRegisterWidgetForEvent</c> only removes a widget from the visual definitions container
/// if its <c>VisualDefinition</c> property is <see langword="null"/>. Consequently, any widget disconnected from the root
/// while retaining a visual definition (such as styled buttons with hover state transitions) remained held in the update
/// loop for the lifetime of the <c>UIContext</c>, preventing garbage collection of disconnected subtrees, views, and ViewModels.
/// v1.3.12 and v1.3.13 hold widgets with <c>TweenPosition</c> set the same way. The transpiler on
/// <c>OnWidgetDisconnectedFromRoot</c> makes those unregistrations unconditional; if a widget is subsequently reattached,
/// <c>OnWidgetConnectedToRoot</c> re-registers it.
/// </para>
/// <para>
/// Up to v1.3.11 the check is the other way around: a widget is removed only while it still has a visual definition
/// (or <c>TweenPosition</c> set). A disconnect removes it, but taking the definition away from a connected widget does not,
/// and once it is disconnected nothing removes it any more. The transpiler on <c>UnRegisterWidgetForEvent</c> drops that
/// check, so a widget that is in the container leaves it whenever it is unregistered. The small
/// <c>OnWidgetVisualDefinitionChanged</c> is not patched: it can be inlined into callers compiled before the patch.
/// </para>
/// <para>
/// The update containers are fields of their own up to v1.3.11, an array indexed by container type in v1.3.12 and v1.3.13,
/// and a dictionary keyed by container type from v1.3.14.
/// </para>
/// </summary>
internal static class EventManagerPatch
{
    private const string WidgetContainers = "TaleWorlds.GauntletUI.EventManager:_widgetContainers";

    /// <summary>The containers that keep a widget only while its state says it belongs there; TweenPosition is gone from v1.3.14.</summary>
    private static readonly string[] StateContainers = ["VisualDefinition", "TweenPosition"];

    public static void Patch(Harmony harmony)
    {
        // From v1.3.12 a disconnect leaves the widget behind; up to v1.3.11 taking its state away does
        var (target, transpiler) = AccessTools2.DeclaredField(WidgetContainers, logErrorInTrace: false) is not null
            ? ("OnWidgetDisconnectedFromRoot", nameof(OnWidgetDisconnectedFromRootTranspiler))
            : ("UnRegisterWidgetForEvent", nameof(UnRegisterWidgetForEventTranspiler));

        if (!harmony.TryPatch(
                AccessTools2.DeclaredMethod($"TaleWorlds.GauntletUI.EventManager:{target}"),
                transpiler: AccessTools2.DeclaredMethod(typeof(EventManagerPatch), transpiler)))
        {
            Trace.TraceWarning("UIExtenderEx: could not patch EventManager.{0}; widgets can stay updated after they no longer belong in the updates", target);
        }
    }

    /// <summary>Resolves the enum integer value of a <c>WidgetContainer.ContainerType</c> member loaded as an IL constant; null where the game has no such member.</summary>
    private static int? ContainerType(string name) =>
        AccessTools2.TypeByName("TaleWorlds.GauntletUI.WidgetContainer+ContainerType") is { IsEnum: true } type && Enum.IsDefined(type, name)
            ? (int) Enum.Parse(type, name)
            : null;

    private static IEnumerable<CodeInstruction> Unchanged(List<CodeInstruction> instructions, string method, string reason)
    {
        Trace.TraceWarning("UIExtenderEx: EventManager.{0} left as it is ({1}); widgets can stay updated after they no longer belong in the updates", method, reason);
        return instructions;
    }

    // ---------------------------------------------------------------- v1.3.12+: a disconnect

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> OnWidgetDisconnectedFromRootTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        const string method = "OnWidgetDisconnectedFromRoot";
        var list = instructions.ToList();
        var unregister = AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.EventManager:UnRegisterWidgetForEvent");
        var replacement = AccessTools2.DeclaredMethod(typeof(EventManagerPatch), nameof(UnregisterUnconditionally));
        if (unregister is null || replacement is null || Unregistrations is not { Count: > 0 } unregistrations)
            return Unchanged(list, method, "a member it changes was not found");

        // this, the container type as a constant, the widget, the call: one call for each of those containers
        var calls = Enumerable.Range(2, Math.Max(0, list.Count - 2))
            .Where(i => list[i].Calls(unregister) && unregistrations.Keys.Any(x => list[i - 2].LoadsConstant(x)))
            .ToList();
        if (calls.Count != unregistrations.Count)
            return Unchanged(list, method, $"{calls.Count} calls unregistering from those updates, {unregistrations.Count} expected");

        // Same stack: the manager, the container type, the widget
        foreach (var call in calls)
        {
            list[call].opcode = OpCodes.Call;
            list[call].operand = replacement;
        }
        return list;
    }

    /// <summary>In place of <c>UnRegisterWidgetForEvent(type, widget)</c> on a disconnect: removed whatever the widget's state.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnregisterUnconditionally(EventManager eventManager, int containerType, Widget widget)
    {
        var unregistration = Unregistrations![containerType];
        var container = unregistration.Container(eventManager);
        // v1.3.12 and v1.3.13 lock the container around every change; v1.3.14+ lock nothing, so the lock costs nothing there
        lock (container)
            unregistration.Remove(container, widget);
    }

    /// <summary>How to reach one update container and take a widget out of it, compiled once.</summary>
    private sealed record Unregistration(Func<EventManager, object> Container, Action<object, Widget> Remove);

    /// <summary>By container type; null where the members are not found.</summary>
    private static readonly Dictionary<int, Unregistration>? Unregistrations = CreateUnregistrations();

    private static Dictionary<int, Unregistration>? CreateUnregistrations()
    {
        try
        {
            var containers = AccessTools2.DeclaredField(WidgetContainers, logErrorInTrace: false);
            var containerType = AccessTools2.TypeByName("TaleWorlds.GauntletUI.WidgetContainer+ContainerType");
            var remove = AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.WidgetContainer:Remove");
            if (containers is null || containerType is null || remove?.DeclaringType is not { } widgetContainer)
                return null;

            var unregistrations = new Dictionary<int, Unregistration>();
            foreach (var name in StateContainers)
            {
                if (ContainerType(name) is not { } type)
                    continue;

                var eventManager = Expression.Parameter(typeof(EventManager), "eventManager");
                var field = Expression.Field(eventManager, containers);
                Expression container;
                if (containers.FieldType.IsArray)
                    container = Expression.ArrayIndex(field, Expression.Constant(type)); // WidgetContainer[], v1.3.12 and v1.3.13
                else if (containers.FieldType.GetProperty("Item") is { } item)
                    container = Expression.Property(field, item, Expression.Constant(Enum.ToObject(containerType, type), containerType)); // Dictionary<ContainerType, WidgetContainer>, v1.3.14+
                else
                    return null;

                var untyped = Expression.Parameter(typeof(object), "container");
                var widget = Expression.Parameter(typeof(Widget), "widget");
                unregistrations[type] = new Unregistration(
                    Expression.Lambda<Func<EventManager, object>>(Expression.Convert(container, typeof(object)), eventManager).Compile(),
                    Expression.Lambda<Action<object, Widget>>(Expression.Call(Expression.Convert(untyped, widgetContainer), remove, widget), untyped, widget).Compile());
            }
            return unregistrations;
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: the visual definition updates cannot be reached ({0}); widgets with a visual definition stay updated after they leave the tree", e.Message);
            return null;
        }
    }

    // ---------------------------------------------------------------- up to v1.3.11: a state taken away

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> UnRegisterWidgetForEventTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        const string method = "UnRegisterWidgetForEvent";
        var list = instructions.ToList();

        // Each container's case asks the widget's state first: `widget.VisualDefinition != null && widget.OnVisualDefinitionListIndex != -1`.
        // The state is answered as present, so the index alone decides, as it does for removal itself
        var replacements = new (string Getter, string Replacement)[]
        {
            ("TaleWorlds.GauntletUI.BaseTypes.Widget:get_VisualDefinition", nameof(VisualDefinitionPresent)),
            ("TaleWorlds.GauntletUI.BaseTypes.Widget:get_TweenPosition", nameof(TweenPositionSet)),
        };
        foreach (var (getterName, replacementName) in replacements)
        {
            var getter = AccessTools2.DeclaredMethod(getterName, logErrorInTrace: false);
            if (getter is null)
                continue; // a state this game does not have

            var replacement = AccessTools2.DeclaredMethod(typeof(EventManagerPatch), replacementName);
            var calls = list.Select((x, i) => (x, i)).Where(x => x.x.Calls(getter)).Select(x => x.i).ToList();
            if (replacement is null || calls.Count != 1)
                return Unchanged(list, method, $"{calls.Count} reads of {getterName}, 1 expected");

            // Same stack: the widget in, the state out
            list[calls[0]].opcode = OpCodes.Call;
            list[calls[0]].operand = replacement;
        }
        return list;
    }

    private static readonly object Present = new();

    /// <summary>In place of <c>widget.VisualDefinition</c> when unregistering: not null, whatever the definition.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object VisualDefinitionPresent(Widget _) => Present;

    /// <summary>In place of <c>widget.TweenPosition</c> when unregistering: set, whatever the widget says.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TweenPositionSet(Widget _) => true;
}