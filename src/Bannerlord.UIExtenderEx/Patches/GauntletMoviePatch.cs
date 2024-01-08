﻿using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Patches;

internal static class GauntletMoviePatch
{
    private static readonly ConcurrentDictionary<UIExtenderRuntime, List<string>> WidgetNames = new();

    public static void Register(UIExtenderRuntime runtime, string? autoGenWidgetName)
    {
        if (string.IsNullOrEmpty(autoGenWidgetName))
            return;

        WidgetNames.AddOrUpdate(runtime, _ => [autoGenWidgetName], (_, list) =>
        {
            list.Add(autoGenWidgetName!);
            return list;
        });
    }

    public static void Deregister(UIExtenderRuntime runtime)
    {
        WidgetNames.TryRemove(runtime, out var _);
    }

    public static void Patch(Harmony harmony)
    {
        if (AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.Data.GauntletMovie:Load") is { } methodInfo &&
            methodInfo.GetParameters() is { } @params &&
            @params.Any(p => p.Name == "doNotUseGeneratedPrefabs"))
        {
            harmony.Patch(
                methodInfo,
                prefix: new HarmonyMethod(typeof(GauntletMoviePatch), nameof(LoadPrefix)));
        }
    }

    private static void LoadPrefix(string movieName, ref bool doNotUseGeneratedPrefabs)
    {
        var movies = WidgetNames.SelectMany(kv => kv.Value);
        if (movies.Contains(movieName))
            doNotUseGeneratedPrefabs = true;
    }
}