using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.Runtimes;
using Bannerlord.UIExtenderEx.Utils;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.ResourceManager;

/// <summary>
/// Manages runtime registration of custom Gauntlet widget types and prefabs within <see cref="WidgetFactory"/>.
/// </summary>
public static class WidgetFactoryManager
{
    private static readonly AccessTools.FieldRef<WidgetFactory, IDictionary>? _liveCustomTypes =
        AccessTools2.FieldRefAccess<WidgetFactory, IDictionary>("_liveCustomTypes");
    private static readonly AccessTools.FieldRef<WidgetFactory, IDictionary>? _liveInstanceTracker =
        AccessTools2.FieldRefAccess<WidgetFactory, IDictionary>("_liveInstanceTracker");

    private delegate Widget WidgetConstructor(UIContext uiContext);
    private static readonly ConcurrentDictionary<Type, WidgetConstructor?> WidgetConstructors = new();

    private static readonly object Lock = new();
    private static readonly Dictionary<string, Func<WidgetPrefab?>> CustomTypes = new();
    private static readonly Dictionary<string, Type> BuiltinTypes = new();

    /// <summary>
    /// Key cache of <see cref="CustomTypes"/>, read lock-free and atomically updated under lock.
    /// Evaluated on every node during template traversal in <c>WidgetTemplate.CreateWidgets</c> and <c>WidgetTemplate.OnRelease</c>.
    /// </summary>
    private static volatile HashSet<string> _customTypeNames = new();

    /// <summary>
    /// Tracks instantiated <see cref="WidgetPrefab"/> instances and active reference counts per <see cref="WidgetFactory"/>.
    /// Maintained per factory to isolate instances across UI resource reloads.
    /// </summary>
    private sealed class LiveCopies
    {
        public readonly Dictionary<string, WidgetPrefab> Prefabs = new();
        public readonly Dictionary<string, int> Users = new();
    }

    private static readonly ConditionalWeakTable<WidgetFactory, LiveCopies> Live = new();

    // Tracks active factories to invalidate cached prefabs upon subsequent registrations.
    private static readonly List<WeakReference<WidgetFactory>> Factories = [];

    public static WidgetPrefab? Create(XmlDocument doc)
    {
        return WidgetPrefabPatch.LoadFromDocument(
            UIResourceManager.WidgetFactory.PrefabExtensionContext,
            UIResourceManager.WidgetFactory.WidgetAttributeContext,
            string.Empty,
            doc);
    }
    public static WidgetPrefab? Create(string name, XmlDocument doc)
    {
        return WidgetPrefabPatch.LoadFromDocument(
            UIResourceManager.WidgetFactory.PrefabExtensionContext,
            UIResourceManager.WidgetFactory.WidgetAttributeContext,
            name,
            doc);
    }

    public static void Register(Type widgetType)
    {
        lock (Lock)
        {
            // Avoid duplicate registrations when callers register types repeatedly across screen initializations.
            if (BuiltinTypes.TryGetValue(widgetType.Name, out var registered) && registered == widgetType)
                return;

            BuiltinTypes[widgetType.Name] = widgetType;
        }

        // Invalidate cached environments and notify listeners that the widget registry has expanded.
        Trace.TraceInformation("UIExtenderEx: widget class '{0}' registered from '{1}'", widgetType.FullName, widgetType.Assembly.GetName().Name);
        WidgetInfoTable.Add([widgetType]);
        PrefabSource.RaiseEnvironmentChanged();
    }
    /// <summary>
    /// Registers a custom widget prefab factory delegate under <paramref name="name"/>.
    /// Replaces vanilla prefabs or prior registrations, evicting cached instances for subsequent instantiations.
    /// </summary>
    public static void Register(string name, Func<WidgetPrefab?> create)
    {
        Trace.TraceInformation("UIExtenderEx: prefab '{0}' registered at runtime", name);
        lock (Lock)
        {
            CustomTypes[name] = create;
            if (!_customTypeNames.Contains(name))
                _customTypeNames = new HashSet<string>(_customTypeNames) { name };
            foreach (var factory in KnownFactories())
                Forget(factory, [name]);
        }
        PrefabSource.RaiseRegistered(name);
    }

    /// <summary>
    /// Evicts cached instances of the specified prefabs from factory caches, forcing the next instantiation to re-parse.
    /// Allows newly enabled extensions or modifications to take effect without restarting the application.
    /// </summary>
    public static void ReloadOnNextUse(IEnumerable<string> prefabNames) => ReloadOnNextUse(UIResourceManager.WidgetFactory, prefabNames);

    internal static void ReloadOnNextUse(WidgetFactory? widgetFactory, IEnumerable<string> prefabNames)
    {
        var names = prefabNames.ToList();
        if (names.Count == 0)
            return;

        if (widgetFactory is not null)
        {
            lock (Lock)
                Forget(widgetFactory, names);
        }

        // Notify subscribers that cached runtime prefabs require recompilation or invalidation.
        PrefabSource.RaiseReloadRequested(names);
    }

    // Evicts cached prefab instances from both native Gauntlet dictionaries and internal live copy tracking.
    private static void Forget(WidgetFactory widgetFactory, IReadOnlyList<string> names)
    {
        try
        {
            var live = _liveCustomTypes?.Invoke(widgetFactory);
            var tracker = _liveInstanceTracker?.Invoke(widgetFactory);
            var ours = Live.TryGetValue(widgetFactory, out var copies) ? copies : null;
            foreach (var name in names)
            {
                // Native OnUnload validates key presence prior to disposal; clearing entries avoids stale eviction faults.
                live?.Remove(name);
                tracker?.Remove(name);
                ours?.Prefabs.Remove(name);
                ours?.Users.Remove(name);
            }
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: could not forget parsed prefabs: {0}", e.Message);
        }
    }

    private static LiveCopies CopiesOf(WidgetFactory widgetFactory) => Live.GetValue(widgetFactory, x =>
    {
        Factories.Add(new(x));
        return new();
    });

    private static List<WidgetFactory> KnownFactories()
    {
        Factories.RemoveAll(x => !x.TryGetTarget(out _));
        var factories = new List<WidgetFactory>(Factories.Count + 1);
        foreach (var reference in Factories)
        {
            if (reference.TryGetTarget(out var factory))
                factories.Add(factory);
        }
        // Include the active UIResourceManager.WidgetFactory even if it has not yet been queried through UIExtenderEx.
        if (UIResourceManager.WidgetFactory is { } current && !factories.Contains(current))
            factories.Add(current);
        return factories;
    }

    /// <summary>Attempts to retrieve a runtime-registered built-in widget class.</summary>
    internal static bool TryGetRegisteredBuiltinType(string name, [MaybeNullWhen(false)] out Type type)
    {
        lock (Lock)
            return BuiltinTypes.TryGetValue(name, out type);
    }

    internal static bool IsRegisteredCustomType(string name) => _customTypeNames.Contains(name);

    /// <summary>
    /// Gets the names of all custom prefabs registered at runtime.
    /// </summary>
    internal static IEnumerable<string> GetRegisteredCustomTypeNames()
    {
        lock (Lock)
            return CustomTypes.Keys.ToList();
    }

    /// <summary>
    /// Attempts to retrieve or instantiate a registered custom prefab for the specified <paramref name="widgetFactory"/>.
    /// Increments the active reference count upon retrieval.
    /// </summary>
    internal static bool TryGetRegisteredCustomType(WidgetFactory widgetFactory, string name, [MaybeNullWhen(false)] out WidgetPrefab prefab)
    {
        Func<WidgetPrefab?>? create;
        lock (Lock)
        {
            var copies = CopiesOf(widgetFactory);
            if (copies.Prefabs.TryGetValue(name, out prefab))
            {
                copies.Users[name]++;
                return true;
            }

            if (!CustomTypes.TryGetValue(name, out create))
                return false;
        }

        // Instantiate outside the lock to prevent re-entrant deadlocks during prefab parsing and event dispatch.
        if (create() is not { } created)
        {
            prefab = null;
            return false;
        }

        lock (Lock)
        {
            var copies = CopiesOf(widgetFactory);
            // Double-checked locking: reuse instance if concurrently parsed by another thread.
            if (copies.Prefabs.TryGetValue(name, out prefab))
            {
                copies.Users[name]++;
                return true;
            }

            copies.Prefabs[name] = created;
            copies.Users[name] = 1;
            prefab = created;
            return true;
        }
    }
    public static void CreateAndRegister(string name, XmlDocument xmlDocument) => Register(name, () => Create($"{name}.xml", xmlDocument));

    public static void Patch(Harmony harmony)
    {
        // Patch individual methods independently so isolated failures do not abort remaining interceptors.
        var customTypes = harmony.TryPatch(
            AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:GetCustomType"),
            prefix: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(GetCustomTypePrefix)));
        customTypes &= harmony.TryPatch(
            AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:IsCustomType"),
            prefix: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(IsCustomTypePrefix)));
        if (!customTypes)
            MessageUtils.DisplayUserWarning("Failed to patch WidgetFactory! Screen elements that mods add will not appear.");

        if (!harmony.TryPatch(
                AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:CreateBuiltinWidget"),
                prefix: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(CreateBuiltinWidgetPrefix))))
        {
            MessageUtils.DisplayUserWarning("Failed to patch WidgetFactory! Screen elements that mods add may appear empty or not respond.");
        }

        // Expose registered widget types to allow compiled prefab providers to discover injected types during code generation.
        if (!harmony.TryPatch(
                AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:GetWidgetTypes"),
                postfix: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(GetWidgetTypesPostfix))))
        {
            MessageUtils.DisplayUserWarning("Failed to patch WidgetFactory.GetWidgetTypes! Some screens that mods change may open more slowly.");
        }

#pragma warning disable BHA0001
        harmony.TryPatch(
            AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:OnUnload"),
            prefix: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(OnUnloadPrefix)));

        // IsCustomType (14 bytes) and OnUnload (92 bytes) are small methods prone to JIT inlining by RyuJIT.
        // Applying blank transpilers forces JIT recompilation of call sites, ensuring prefixes execute reliably.
        harmony.TryPatch(
            AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate:CreateWidgets"),
            transpiler: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
        harmony.TryPatch(
            AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate:OnRelease"),
            transpiler: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
        harmony.TryPatch(
            AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.Data.GauntletMovie:Release"),
            transpiler: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
#pragma warning restore BHA0001
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GetWidgetTypesPostfix(ref IEnumerable<string> __result)
    {
        List<string> registered;
        lock (Lock)
            registered = BuiltinTypes.Keys.Concat(CustomTypes.Keys).ToList();
        __result = __result.Concat(registered);
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CreateBuiltinWidgetPrefix(UIContext context, string typeName, ref object? __result)
    {
        if (!TryGetRegisteredBuiltinType(typeName, out var type))
            return true;

        var ctor = WidgetConstructors.GetOrAdd(type, static x => AccessTools2.GetDeclaredConstructorDelegate<WidgetConstructor>(x, [typeof(UIContext)]));
        if (ctor is null)
            return true;

        __result = ctor(context);
        return false;
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsCustomTypePrefix(string typeName, ref bool __result)
    {
        if (!IsRegisteredCustomType(typeName))
            return true;

        __result = true;
        return false;
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool GetCustomTypePrefix(WidgetFactory __instance, string typeName, ref WidgetPrefab __result)
    {
        // Ensure the querying factory is tracked for future eviction cycles.
        lock (Lock)
            CopiesOf(__instance);

        // If the native factory already holds an active custom type or the type is unregistered, allow vanilla resolution.
        if (_liveCustomTypes?.Invoke(__instance) is { } ____liveCustomTypes &&
            ____liveCustomTypes.Contains(typeName) || !IsRegisteredCustomType(typeName))
            return true;

        if (TryGetRegisteredCustomType(__instance, typeName, out var widgetPrefab))
        {
            __result = widgetPrefab;
            return false;
        }

        return true;
    }

    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool OnUnloadPrefix(WidgetFactory __instance, string typeName)
    {
        // Native OnRelease traverses every custom type node; decrement reference count only for registered custom prefabs.
        if (!IsRegisteredCustomType(typeName))
            return true;

        lock (Lock)
        {
            if (!Live.TryGetValue(__instance, out var copies) || !copies.Prefabs.ContainsKey(typeName))
                return true;

            if (--copies.Users[typeName] == 0)
            {
                copies.Prefabs.Remove(typeName);
                copies.Users.Remove(typeName);
            }
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<CodeInstruction> BlankTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;
}