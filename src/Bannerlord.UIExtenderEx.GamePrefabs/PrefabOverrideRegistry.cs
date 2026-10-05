using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Detects prefabs overridden across loaded module directories.
/// <para>
/// When multiple modules provide identical prefab files under <c>GUI/Prefabs</c>, Gauntlet loads the file from the
/// highest-priority module. Native pre-compiled prefabs bypass this filesystem resolution.
/// This registry discovers overridden prefabs to invalidate stale pre-compiled variants and force re-parsing from disk.
/// </para>
/// </summary>
public static class PrefabOverrideRegistry
{
    private static readonly AccessTools.FieldRef<WidgetFactory, ResourceDepot>? ResourceDepotOf =
        AccessTools2.FieldRefAccess<WidgetFactory, ResourceDepot>("_resourceDepot");

    private static readonly AccessTools.FieldRef<WidgetFactory, string>? ResourceFolderOf =
        AccessTools2.FieldRefAccess<WidgetFactory, string>("_resourceFolder");

    private static readonly AccessTools.FieldRef<ResourceDepot, List<ResourceDepotLocation>>? ResourceLocationsOf =
        AccessTools2.FieldRefAccess<ResourceDepot, List<ResourceDepotLocation>>("_resourceLocations");

    private static readonly object Lock = new();
    private static WidgetFactory? _factory;
    private static HashSet<string> _overridden = new(StringComparer.Ordinal);

    /// <summary>
    /// Determines whether any of the specified <paramref name="prefabNames"/> are overridden across module directories.
    /// </summary>
    public static bool ContainsOverridden(WidgetFactory widgetFactory, IEnumerable<string> prefabNames)
    {
        var overridden = GetOverridden(widgetFactory);
        return overridden.Count != 0 && prefabNames.Any(overridden.Contains);
    }

    /// <summary>Clears the cached set of overridden prefabs. Intended for testing.</summary>
    public static void ClearCache()
    {
        lock (Lock)
        {
            _factory = null;
            _overridden = new(StringComparer.Ordinal);
        }
    }

    private static HashSet<string> GetOverridden(WidgetFactory widgetFactory)
    {
        lock (Lock)
        {
            // Cache results per WidgetFactory instance across resource reloads.
            if (ReferenceEquals(_factory, widgetFactory))
                return _overridden;

            _factory = widgetFactory;
            _overridden = Compute(widgetFactory);
            return _overridden;
        }
    }

    /// <summary>
    /// Scans resource locations in <paramref name="widgetFactory"/> to discover duplicate prefab files across module directories.
    /// </summary>
    private static HashSet<string> Compute(WidgetFactory widgetFactory)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if (ResourceDepotOf is null || ResourceFolderOf is null || ResourceLocationsOf is null)
                return result;

            var locations = ResourceLocationsOf(ResourceDepotOf(widgetFactory));
            if (locations is null || locations.Count < 2)
                return result;

            var stopwatch = Stopwatch.StartNew();
            var resourceFolder = ResourceFolderOf(widgetFactory);
            // Compare raw file names before normalization to distinguish between dotted and underscored file names on disk.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var replaced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var location in locations)
            {
                var directory = Path.Combine(location.FullPath.Replace('/', Path.DirectorySeparatorChar), resourceFolder);
                if (!Directory.Exists(directory))
                    continue;

                // Match files strictly ending with .xml to avoid development backup copies (e.g., .xml_dev).
                var names = Directory.GetFiles(directory, "*.xml", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileNameWithoutExtension)
                    .Distinct(StringComparer.Ordinal);
                foreach (var name in names)
                {
                    if (seen.Add(name))
                        continue;
                    replaced.Add(name);
                    result.Add(PrefabNames.Normalize(name));
                }
            }

            if (replaced.Count > 0)
                Trace.TraceInformation("UIExtenderEx: {0} prefab(s) replaced by a module in {1} ms, their movies will not use the game's pre-compiled prefabs: {2}",
                    replaced.Count, stopwatch.ElapsedMilliseconds, string.Join(", ", [.. replaced.OrderBy(x => x, StringComparer.Ordinal)]));
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: could not look for replaced prefabs, a module's prefab override may be ignored: {0}", e.Message);
        }

        return result;
    }
}