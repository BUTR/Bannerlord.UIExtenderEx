using Bannerlord.UIExtenderEx.Runtimes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Provides access to enabled <see cref="ViewModel"/> mixins grouped by target ViewModel type in deterministic resolution order.
/// <para>
/// Maintains identical registration order between <see cref="MixinMemberResolver"/> member selection and <see cref="PrefabFingerprint"/>
/// hash generation. Because mixin member resolution awards precedence to the latest registered mixin, preserves explicit registration order
/// so cache invalidation accurately tracks mixin precedence changes.
/// </para>
/// </summary>
public static class MixinRegistrations
{
    private sealed record ReachableEntry(int Version, IReadOnlyList<KeyValuePair<Type, IReadOnlyList<Type>>> Registrations);
    private static readonly Dictionary<Type, ReachableEntry> Reachable = new();
    private static readonly object ReachableLock = new();

    /// <summary>
    /// Traverses and returns all ViewModel target types reachable from the root ViewModel through properties and binding lists.
    /// Traverses child properties and mixin members to discover newly enabled mixins before code generation runs.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<Type, IReadOnlyList<Type>>> ForRoot(Type root)
    {
        lock (ReachableLock)
        {
            var version = UIEnvironmentVersion.Version;
            if (Reachable.TryGetValue(root, out var entry) && entry.Version == version)
                return entry.Registrations;

            var visited = new HashSet<Type>();
            var pending = new Queue<Type>();
            var registrations = new List<KeyValuePair<Type, IReadOnlyList<Type>>>();
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                var target = pending.Dequeue();
                if (!visited.Add(target))
                    continue;
                if (typeof(IMBBindingList).IsAssignableFrom(target))
                {
                    var arguments = target.GetGenericArguments();
                    if (arguments.Length > 0)
                        Enqueue(arguments[0]);
                    continue;
                }

                var mixins = ForViewModel(target).ToList();
                if (mixins.Count > 0)
                    registrations.Add(new(target, mixins));
                foreach (var property in target.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    Enqueue(property.PropertyType);
                foreach (var mixin in mixins)
                    foreach (var property in mixin.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                        if (property.IsDefined(typeof(DataSourceProperty), true))
                            Enqueue(property.PropertyType);
            }

            void Enqueue(Type type)
            {
                if (typeof(ViewModel).IsAssignableFrom(type) || typeof(IMBBindingList).IsAssignableFrom(type))
                    pending.Enqueue(type);
            }

            var ordered = registrations.OrderBy(x => x.Key.AssemblyQualifiedName, StringComparer.Ordinal).ToList();
            Reachable[root] = new(version, ordered);
            return ordered;
        }
    }

    /// <summary>
    /// Returns enabled mixins registered for the specified <paramref name="viewModelType"/> in resolution precedence order.
    /// </summary>
    public static IEnumerable<Type> ForViewModel(Type viewModelType) =>
        MixinSource.GetEnabledMixinTypes(viewModelType);

    /// <summary>
    /// Enumerates all ViewModel types possessing at least one enabled mixin, alongside their registered mixin types in resolution order.
    /// </summary>
    public static IEnumerable<KeyValuePair<Type, IReadOnlyList<Type>>> All()
    {
        var targets = MixinSource.GetMixinTargetTypes()
            .Distinct()
            .OrderBy(x => x.AssemblyQualifiedName ?? x.FullName ?? x.Name, StringComparer.Ordinal);

        foreach (var target in targets)
        {
            var mixins = ForViewModel(target).ToList();
            if (mixins.Count > 0)
                yield return new(target, mixins);
        }
    }

    /// <summary>Enumerates all unique enabled mixin types across all targets.</summary>
    public static IEnumerable<Type> AllMixinTypes() => All().SelectMany(x => x.Value).Distinct();
}