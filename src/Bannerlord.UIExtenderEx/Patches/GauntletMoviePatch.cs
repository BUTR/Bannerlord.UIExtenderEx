using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Patches;

internal static class GauntletMoviePatch
{
    private static readonly ConcurrentDictionary<UIExtenderRuntime, List<string>> _widgetNames = new();
    private static readonly ConcurrentDictionary<Type, Type[]> _widgetChildCache = new();
    private static readonly AccessTools.FieldRef<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>? _generatedPrefabs =
        AccessTools2.FieldRefAccess<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>("_generatedPrefabs");

    public static void Register(UIExtenderRuntime runtime, string? autoGenWidgetName)
    {
        if (string.IsNullOrEmpty(autoGenWidgetName))
            return;

        _widgetNames.AddOrUpdate(runtime, _ => [autoGenWidgetName], (_, list) =>
        {
            list.Add(autoGenWidgetName!);
            return list;
        });
    }

    public static void Deregister(UIExtenderRuntime runtime)
    {
        _widgetNames.TryRemove(runtime, out var _);
    }

    public static void Patch(Harmony harmony)
    {
        if (AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.Data.GauntletMovie:Load") is { } mi && mi.GetParameters() is { } p && p.Any(x => x.Name == "doNotUseGeneratedPrefabs"))
        {
            harmony.Patch(
                mi,
                prefix: new HarmonyMethod(typeof(GauntletMoviePatch), nameof(LoadPrefix)));
        }
    }

    private static void LoadPrefix(WidgetFactory widgetFactory, string movieName, IViewModel? datasource, ref bool doNotUseGeneratedPrefabs)
    {
        static IEnumerable<string> GetAllInvolvedAutoGenNames(WidgetFactory widgetFactory, string movieName, IViewModel? datasource)
        {
            static IEnumerable<Type> GetChildWidgets(Type widgetType)
            {
                var children = _widgetChildCache.GetOrAdd(widgetType, static x => x.GetFields(AccessTools.all).Select(x => x.FieldType).Where(x => x.IsSubclassOf(typeof(Widget))).Distinct().ToArray());
                foreach (var childWidgetType in children.Where(x => x != widgetType))
                {
                    foreach (var childChildWidgetType in GetChildWidgets(childWidgetType).Where(x => x != widgetType && x != childWidgetType))
                    {
                        yield return childChildWidgetType;
                    }
                }
            }

            if (_generatedPrefabs?.Invoke(widgetFactory.GeneratedPrefabContext) is { } generatedPrefabs)
            {
                const string create = "Create";
                var variantName = datasource != null ? datasource.GetType().FullName : "Default";
                if (generatedPrefabs.TryGetValue(movieName, out var dict2) && dict2.TryGetValue(variantName, out var creator) && AccessTools2.TypeByName(creator.Method.Name.Remove(0, create.Length)) is { } type)
                {
                    var widgets = new List<Type> { type }.Concat(GetChildWidgets(type));
                    var widgetNames = widgets.Select(x => x.Name);
                    var autoGenNames = widgetNames.Where(x => x.Contains("__"));
                    return autoGenNames.Select(x => x.Split(["__"], StringSplitOptions.None)[0]);
                }
            }
            /* This implementation actually created the Widget, but it seems that game didn't intend for that
            var variantName = datasource == null ? "Default" : datasource.GetType().FullName;
            var data = datasource == null ? new Dictionary<string, object>() : new() { {"DataSource", datasource} };
            if (widgetFactory.GeneratedPrefabContext.InstantiatePrefab(context, movieName, variantName, data) is { } autogenResult)
            {
                var autoGen = autogenResult.Root;
                autoGen.DisableRender = true;
                autoGen.IsVisible = false;
                autoGen.UpdateChildrenStates = false;
                var widgetNames = new HashSet<string> { autoGen.GetType().Name };
                CheckChildrenAutoGens(ref widgetNames, autoGen);
                var autoGenNames = widgetNames.Where(x => x.Contains("__")).ToArray();
                return autoGenNames.Select(x => x.Split(["__"], StringSplitOptions.None)[0]);
            }
            */

            return Enumerable.Empty<string>();
        }

        var moviesPatched = new HashSet<string>(UIExtender.GetAllRuntimes().SelectMany(x => x.PrefabComponent.GetMoviesToPatch()));
        var moviesInvolved = new HashSet<string>(GetAllInvolvedAutoGenNames(widgetFactory, movieName, datasource));
        if (moviesInvolved.Overlaps(moviesPatched))
            doNotUseGeneratedPrefabs = true;

        var moviesBlacklisted = _widgetNames.SelectMany(kv => kv.Value);
        if (moviesBlacklisted.Contains(movieName))
            doNotUseGeneratedPrefabs = true;
    }
}