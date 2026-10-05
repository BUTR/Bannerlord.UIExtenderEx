using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

/// <summary>
/// Provides extension methods and resolution utilities over <see cref="WidgetFactory"/> supporting dynamic runtime registrations.
/// <para>
/// Extends TaleWorlds factory lookup logic to integrate runtime-registered builtin widgets and custom prefabs
/// introduced by UIExtenderEx (<see cref="IWidgetRegistrations"/>), ensuring compiled code generators and dependency
/// analyzers discover modded types seamlessly alongside base game widgets.
/// </para>
/// </summary>
public static class WidgetFactoryLookup
{
    extension(WidgetFactory widgetFactory)
    {
        public bool IsBuiltinTypeIncludingRegistered(string name) =>
            !widgetFactory.IsCustomTypeIncludingRegistered(name) &&
            (CodeGeneratorEnvironment.Registrations.TryGetRegisteredBuiltinType(name, out _) || widgetFactory.IsBuiltinType(name));

        public Type GetBuiltinTypeIncludingRegistered(string name)
        {
            var type = CodeGeneratorEnvironment.Registrations.TryGetRegisteredBuiltinType(name, out var registered) ? registered : widgetFactory.GetBuiltinType(name);
            TypeDependencies.Inspect(type);
            TypeDependencies.InspectConstructors(type);
            return type;
        }

        public bool IsCustomTypeIncludingRegistered(string name) =>
            CodeGeneratorEnvironment.Registrations.IsRegisteredCustomType(name) || widgetFactory.IsCustomType(name);

        /// <summary>
        /// Determines whether <paramref name="name"/> represents an unknown type (neither a builtin widget nor a registered custom prefab).
        /// </summary>
        public bool IsUnknownTypeIncludingRegistered(string name) =>
            !widgetFactory.IsCustomTypeIncludingRegistered(name)
            && !CodeGeneratorEnvironment.Registrations.TryGetRegisteredBuiltinType(name, out _)
            && !widgetFactory.IsBuiltinType(name);

        /// <summary>
        /// Attempts to load the custom prefab with the specified name, tracking usage within the active <see cref="PrefabLease"/>.
        /// </summary>
        /// <param name="name">The prefab name to resolve.</param>
        /// <param name="prefab">When found, receives the resolved <see cref="WidgetPrefab"/>; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the custom prefab was loaded successfully; otherwise, <see langword="false"/>.</returns>
        public bool TryGetCustomTypeIncludingRegistered(string name, [MaybeNullWhen(false)] out WidgetPrefab prefab)
        {
            prefab = Load(widgetFactory, name, out _);
            return prefab is not null;
        }

        /// <summary>
        /// Resolves the underlying widget CLR type for a given template name, unwrapping nested prefab roots until a concrete widget type is found.
        /// </summary>
        /// <param name="name">The initial prefab or widget type name.</param>
        /// <param name="type">When resolved, receives the target <see cref="Type"/>; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if resolution succeeded without cycle detection; otherwise, <see langword="false"/>.</returns>
        public bool TryGetWidgetTypeWithinPrefabRoots(string name, [MaybeNullWhen(false)] out Type type)
        {
            HashSet<string>? visited = null;
            var current = name;
            while (true)
            {
                if (!widgetFactory.IsCustomTypeIncludingRegistered(current))
                {
                    if (CodeGeneratorEnvironment.Registrations.TryGetRegisteredBuiltinType(current, out type))
                    {
                        TypeDependencies.Inspect(type);
                        TypeDependencies.InspectConstructors(type);
                        return true;
                    }
                    if (widgetFactory.IsBuiltinType(current))
                    {
                        type = widgetFactory.GetBuiltinType(current);
                        TypeDependencies.Inspect(type);
                        TypeDependencies.InspectConstructors(type);
                        return true;
                    }
                    type = typeof(TaleWorlds.GauntletUI.BaseTypes.Widget);
                    return true;
                }

                visited ??= new(StringComparer.Ordinal);
                if (!visited.Add(current) || !widgetFactory.TryGetCustomTypeIncludingRegistered(current, out var prefab) || prefab.RootTemplate is not { } root)
                {
                    type = null;
                    return false;
                }
                current = root.Type;
            }
        }

        /// <summary>
        /// Retrieves the custom prefab by name, throwing a descriptive <see cref="InvalidOperationException"/> if resolution fails.
        /// </summary>
        /// <param name="name">The prefab name to resolve.</param>
        /// <returns>The resolved <see cref="WidgetPrefab"/> instance.</returns>
        public WidgetPrefab GetCustomTypeIncludingRegistered(string name)
        {
            if (Load(widgetFactory, name, out var failure) is { } prefab)
                return prefab;

            throw new InvalidOperationException(failure ??
                $"'{name}' is neither a widget class nor a prefab known to the WidgetFactory. If a mod provides it, it has to be registered through UIExtenderEx's WidgetFactoryManager before the movie loads.");
        }
    }

    /// <summary>
    /// Loads a prefab by name from the factory or active lease, capturing diagnostics if loading fails.
    /// </summary>
    private static WidgetPrefab? Load(WidgetFactory widgetFactory, string name, out string? failure)
    {
        failure = null;
        try
        {
            // A pinning lease has read this name already and its hash was taken from that object. Serving it again
            // is what makes the code that is generated and the fingerprint that judges it describe one and the same parse;
            // asking the factory again could get a different document back.
            if (PrefabLease.TryGetPinned(widgetFactory, name, out var pinned))
                return pinned;

            if (CodeGeneratorEnvironment.Registrations.TryGetRegisteredCustomType(widgetFactory, name, out var registered))
            {
                PrefabLease.Track(widgetFactory, name, registered);
                return registered;
            }

            // Asked only when the factory knows the name: the game asserts on an unknown one
            if (!widgetFactory.IsCustomType(name))
                return null;

            var prefab = widgetFactory.GetCustomType(name);
            if (prefab is null)
                return null;

            if (prefab.RootTemplate is not null)
            {
                PrefabLease.Track(widgetFactory, name, prefab);
                return prefab;
            }

            // The use was acquired all the same and has to be released; what was read is no prefab anyone can use, so it is not pinned
            PrefabLease.Track(widgetFactory, name, null);
            failure = Describe(widgetFactory, name, "it has no root widget");
            return null;
        }
        catch (Exception e)
        {
            failure = Describe(widgetFactory, name, e.Message);
            Trace.TraceWarning("UIExtenderEx: {0}", failure);
            return null;
        }
    }

    private static string Describe(WidgetFactory widgetFactory, string name, string reason)
    {
        // Guarded: GetCustomTypePath asserts on a name it does not have, and an assert dialog is not what a prefab that
        // is about to fall back to XML deserves
        var path = string.Empty;
        try
        {
            if (widgetFactory.IsCustomType(name))
                path = widgetFactory.GetCustomTypePath(name);
        }
        catch (Exception) { /* nothing better to say than where it is */ }
        var where = string.IsNullOrEmpty(path) ? "registered at runtime" : $"'{path}{name}.xml'";
        return $"'{name}' is registered as a prefab ({where}) but cannot be loaded as one: {reason}. " +
               "A UIExtenderEx patch fragment placed under GUI/Prefabs is registered as a prefab by its file name; keeping it outside GUI/Prefabs avoids that.";
    }

    /// <summary>
    /// Scopes and balances live prefab usage counters in <see cref="WidgetFactory"/> during compilation and analysis passes.
    /// <para>
    /// Prevents prefab memory retention and ensures runtime cache invalidations remain effective.
    /// When configured as a pinning lease, caches parsed <see cref="WidgetPrefab"/> instances to guarantee consistency
    /// between fingerprinting and code generation.
    /// </para>
    /// </summary>
    public sealed class PrefabLease : IDisposable
    {
        [ThreadStatic]
        private static PrefabLease? _current;

        private readonly WidgetFactory _factory;
        private readonly List<string> _acquired = [];
        private readonly Dictionary<string, WidgetPrefab>? _pinned;
        private readonly PrefabLease? _outer;
        private bool _disposed;

        /// <summary>Gets the active lease on the current thread, if any.</summary>
        public static PrefabLease? Current => _current;

        /// <summary>Gets the widget factory managed by this lease.</summary>
        public WidgetFactory Factory => _factory;

        /// <summary>Gets the list of prefab names acquired during the lease lifetime.</summary>
        public IReadOnlyList<string> Acquired => _acquired;

        /// <summary>Gets the dictionary of pinned prefabs cached within this lease, or <see langword="null"/> if not pinning.</summary>
        public IReadOnlyDictionary<string, WidgetPrefab>? Pinned => _pinned;

        private PrefabLease(WidgetFactory factory, bool pin)
        {
            _factory = factory;
            _pinned = pin ? new(StringComparer.Ordinal) : null;
            _outer = _current;
        }

        /// <summary>
        /// Begins a new scoped prefab lease over the specified widget factory.
        /// </summary>
        /// <param name="factory">The widget factory to manage.</param>
        /// <param name="pin">If <see langword="true"/>, pins loaded prefabs in memory for repeat queries.</param>
        /// <returns>A disposable lease token.</returns>
        public static PrefabLease Begin(WidgetFactory factory, bool pin = false)
        {
            var lease = new PrefabLease(factory, pin);
            _current = lease;
            return lease;
        }

        /// <summary>
        /// Attempts to retrieve a pinned prefab instance from the active innermost lease.
        /// </summary>
        internal static bool TryGetPinned(WidgetFactory factory, string name, [MaybeNullWhen(false)] out WidgetPrefab prefab)
        {
            if (_current is { _disposed: false, _pinned: { } pinned } lease && ReferenceEquals(lease._factory, factory))
                return pinned.TryGetValue(name, out prefab);
            prefab = null;
            return false;
        }

        /// <summary>
        /// Records an acquired prefab use and caches the instance if pinning is active.
        /// </summary>
        internal static void Track(WidgetFactory factory, string prefabName, WidgetPrefab? prefab)
        {
            if (_current is not { } lease || !ReferenceEquals(lease._factory, factory))
                return;

            lease._acquired.Add(prefabName);
            if (prefab is not null && lease._pinned is { } pinned)
                pinned[prefabName] = prefab;
        }

        /// <summary>
        /// Disposes the lease, releasing all acquired prefab references back to the widget factory.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (ReferenceEquals(_current, this))
            {
                // Skip past leases that were disposed before their turn, so the chain never points at a dead lease and
                // releasing what a lease acquired never depends on the leases ending in the order they began
                var next = _outer;
                while (next is { _disposed: true })
                    next = next._outer;
                _current = next;
            }

            foreach (var prefabName in _acquired)
            {
                try
                {
                    _factory.OnUnload(prefabName);
                }
                catch (Exception)
                {
                    // Nothing to do about a factory that refuses; the worst case is one prefab left pinned
                }
            }
            _acquired.Clear();
            _pinned?.Clear();
        }
    }
}