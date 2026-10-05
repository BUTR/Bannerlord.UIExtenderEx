using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using TaleWorlds.GauntletUI;

namespace Bannerlord.UIExtenderEx.ResourceManager;

/// <summary>
/// Registers widget classes directly into Gauntlet's internal <see cref="WidgetInfo"/> registry without triggering full assembly rescans.
/// <para>
/// Gauntlet caches widget metadata in an internal dictionary populated during initialization.
/// Direct insertion avoids the multi-hundred millisecond overhead of <see cref="WidgetInfo.Refresh"/> assembly reflection.
/// </para>
/// </summary>
internal static class WidgetInfoTable
{
    private static readonly AccessTools.FieldRef<Dictionary<Type, WidgetInfo>>? WidgetInfos =
        AccessTools2.StaticFieldRefAccess<Dictionary<Type, WidgetInfo>>(typeof(WidgetInfo), "_widgetInfos");

    /// <summary>
    /// Adds the specified widget types to Gauntlet's internal <see cref="WidgetInfo"/> table.
    /// Returns <see langword="false"/> if direct mutation fails and fallback assembly rescanning via <see cref="WidgetInfo.Refresh"/> occurs.
    /// </summary>
    public static bool Add(IEnumerable<Type> widgetTypes)
    {
        var types = widgetTypes.ToList();
        if (TryAdd(types))
            return true;

        var stopwatch = Stopwatch.StartNew();
        WidgetInfo.Refresh();
        Trace.TraceInformation("UIExtenderEx: rescanned every assembly for widget classes in {0} ms, the widget table could not be edited in place", stopwatch.ElapsedMilliseconds);
        return false;
    }

    private static bool TryAdd(List<Type> types)
    {
        try
        {
            if (WidgetInfos is null)
                return false;

            // A null table indicates Gauntlet has not yet initialized its widget cache; subsequent native initialization will discover the types.
            if (WidgetInfos() is not { } table)
                return true;

            foreach (var type in types)
            {
                if (!table.ContainsKey(type))
                    table.Add(type, new(type));
            }
            return true;
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: could not add widget classes to the game's widget table: {0}", e.Message);
            return false;
        }
    }
}