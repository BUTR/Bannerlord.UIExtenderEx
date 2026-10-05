using Bannerlord.UIExtenderEx.ResourceManager;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Xml;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Runtimes;

/// <summary>
/// Exposes patched prefab XML data, widget factory state, and lifecycle invalidation events consumed by prefab runtimes.
/// <para>
/// Events fire on the thread that triggered the underlying operation (typically the main engine UI thread, except when
/// background compilation workers parse prefabs). Event handler exceptions are caught and logged without propagating into
/// TaleWorlds engine calls.
/// </para>
/// </summary>
public static class PrefabSource
{
    /// <summary>
    /// Occurs when a prefab has been parsed into a <see cref="WidgetPrefab"/> from an <see cref="XmlDocument"/> with
    /// all active patches applied.
    /// </summary>
    public static event Action<WidgetPrefab, string, XmlDocument>? Parsed;

    /// <summary>
    /// Occurs when a mod registers a custom prefab definition at runtime, superseding disk XML of the same name.
    /// </summary>
    public static event Action<string>? Registered;

    /// <summary>
    /// Occurs when prefabs are flagged for cache invalidation, forcing subsequent requests to parse XML afresh rather
    /// than reusing cached instances.
    /// </summary>
    public static event Action<IReadOnlyList<string>>? ReloadRequested;

    /// <summary>
    /// Occurs when the UI environment changes in a manner that alters member or binding resolution (such as enabling,
    /// disabling, or deregistering mixins, or registering widget types).
    /// </summary>
    public static event Action? EnvironmentChanged;

    private static int _unheardParses;

    /// <summary>
    /// Gets the count of prefabs parsed before any runtime subscribed to <see cref="Parsed"/>. Missing early parse
    /// events prevents fingerprint tracking and triggers cache misses on initial movie load.
    /// </summary>
    internal static int UnheardParses => Volatile.Read(ref _unheardParses);

    internal static void RaiseParsed(WidgetPrefab prefab, string prefabName, XmlDocument document)
    {
        if (Parsed is not { } handlers)
        {
            if (Interlocked.Increment(ref _unheardParses) == 1)
                Trace.TraceInformation("UIExtenderEx: prefab '{0}' was parsed with no prefab runtime listening", prefabName);
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
            Invoke(handler, nameof(Parsed), () => ((Action<WidgetPrefab, string, XmlDocument>) handler)(prefab, prefabName, document));
    }

    internal static void RaiseRegistered(string prefabName)
    {
        if (Registered is not { } handlers)
            return;
        foreach (var handler in handlers.GetInvocationList())
            Invoke(handler, nameof(Registered), () => ((Action<string>) handler)(prefabName));
    }

    internal static void RaiseReloadRequested(IReadOnlyList<string> prefabNames)
    {
        if (ReloadRequested is not { } handlers)
            return;
        foreach (var handler in handlers.GetInvocationList())
            Invoke(handler, nameof(ReloadRequested), () => ((Action<IReadOnlyList<string>>) handler)(prefabNames));
    }

    internal static void RaiseEnvironmentChanged()
    {
        if (EnvironmentChanged is not { } handlers)
            return;
        foreach (var handler in handlers.GetInvocationList())
            Invoke(handler, nameof(EnvironmentChanged), (Action) handler);
    }

    private static void Invoke(Delegate handler, string eventName, Action call)
    {
        try
        {
            call();
        }
        catch (Exception e)
        {
            Trace.TraceError("UIExtenderEx: a {0} handler of '{1}' failed: {2}", eventName, handler.Method.DeclaringType?.FullName, e);
        }
    }

    /// <summary>
    /// Gets an immutable snapshot of all prefab names currently targeted by active patches across all registered module runtimes.
    /// </summary>
    public static IReadOnlyCollection<string> PatchedPrefabNames =>
        new HashSet<string>(UIExtender.GetAllRuntimes().SelectMany(x => x.PrefabComponent.GetMoviesToPatch()), StringComparer.Ordinal);

    /// <summary>
    /// Attempts to resolve a widget <see cref="Type"/> registered by a mod at runtime under the specified name.
    /// </summary>
    public static bool TryGetRegisteredBuiltinType(string name, [MaybeNullWhen(false)] out Type type) =>
        WidgetFactoryManager.TryGetRegisteredBuiltinType(name, out type);

    /// <summary>
    /// Determines whether a custom prefab is registered at runtime under the specified name.
    /// </summary>
    public static bool IsRegisteredCustomType(string name) => WidgetFactoryManager.IsRegisteredCustomType(name);

    /// <summary>
    /// Attempts to retrieve or instantiate a runtime-registered <see cref="WidgetPrefab"/> from the specified widget factory.
    /// </summary>
    public static bool TryGetRegisteredCustomType(WidgetFactory widgetFactory, string name, [MaybeNullWhen(false)] out WidgetPrefab prefab) =>
        WidgetFactoryManager.TryGetRegisteredCustomType(widgetFactory, name, out prefab);

    /// <summary>
    /// Gets the names of all custom prefabs registered at runtime.
    /// </summary>
    public static IEnumerable<string> RegisteredCustomTypeNames => WidgetFactoryManager.GetRegisteredCustomTypeNames();

    /// <summary>
    /// Registers newly discovered or dynamically compiled widget types directly into Gauntlet's internal widget
    /// lookup table (<c>WidgetInfo._widgetInfos</c>), avoiding costly full-assembly rescans.
    /// </summary>
    public static void AddWidgetTypes(IEnumerable<Type> widgetTypes) => WidgetInfoTable.Add(widgetTypes);
}