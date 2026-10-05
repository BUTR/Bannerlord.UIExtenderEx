using Bannerlord.BUTR.Shared.Utils;

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Extensions;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.Runtimes;
using Bannerlord.UIExtenderEx.Utils;
using Bannerlord.UIExtenderEx.ViewModels;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Components;

/// <summary>
/// Manages runtime registration, lifecycle, and dynamic property/command binding of <see cref="IViewModelMixin"/> types.
/// </summary>
internal class ViewModelComponent
{
    /// <summary>
    /// Registered mixin types indexed by their target <see cref="ViewModel"/> type.
    /// </summary>
    public readonly ConcurrentDictionary<Type, List<Type>> Mixins = new();

    /// <summary>
    /// Cache of active <see cref="IViewModelMixin"/> instances attached to target <see cref="ViewModel"/> instances.
    /// Entries are collected when the underlying view model instance is finalized.
    /// </summary>
    internal readonly ConditionalWeakTable<ViewModel, List<IViewModelMixin>> MixinInstanceCache = new();

    internal readonly ConditionalWeakTable<ViewModel, List<string>> MixinInstanceRefreshFromConstructorCache = new();

    private readonly ConcurrentDictionary<Type, bool> _mixinTypeEnabled = new();
    private readonly ConcurrentDictionary<Type, List<PropertyInfo>> _mixinTypePropertyCache = new();
    private readonly ConcurrentDictionary<Type, List<MethodInfo>> _mixinTypeMethodCache = new();

    /// <summary>
    /// Stores overridden <see cref="ViewModel"/> methods mapped to their corresponding <see cref="BUTRViewModelOverrideAttribute"/> interceptor methods.
    /// </summary>
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<MethodBase, MethodInfo>> _mixinOverrides = new();

    /// <summary>The module name associated with this component.</summary>
    private readonly string _moduleName;

    private static readonly object RetiredLock = new();
    private static ViewModelComponent[] _retired = [];

    /// <summary>
    /// Stores retired components from deregistered modules to keep attached mixins functional until their host view models are finalized.
    /// </summary>
    internal static ViewModelComponent[] Retired => Volatile.Read(ref _retired);

    public ViewModelComponent(string moduleName)
    {
        _moduleName = moduleName;
    }

    /// <summary>
    /// Enables all registered mixin types managed by this component.
    /// </summary>
    public void Enable()
    {
        foreach (var mixinType in _mixinTypeEnabled.Keys)
            _mixinTypeEnabled[mixinType] = true;
        // Toggling mixin states alters data binding resolution; signal environment invalidation to refresh cached prefabs.
        PrefabSource.RaiseEnvironmentChanged();
    }

    /// <summary>
    /// Disables all registered mixin types managed by this component.
    /// </summary>
    public void Disable()
    {
        foreach (var mixinType in _mixinTypeEnabled.Keys)
            _mixinTypeEnabled[mixinType] = false;
        PrefabSource.RaiseEnvironmentChanged();
    }

    /// <summary>
    /// Enables a specific registered mixin type.
    /// </summary>
    /// <param name="mixinType">The mixin type to enable.</param>
    public void Enable(Type mixinType)
    {
        if (_mixinTypeEnabled.ContainsKey(mixinType))
            _mixinTypeEnabled[mixinType] = true;
        PrefabSource.RaiseEnvironmentChanged();
    }

    /// <summary>
    /// Disables a specific registered mixin type.
    /// </summary>
    /// <param name="mixinType">The mixin type to disable.</param>
    public void Disable(Type mixinType)
    {
        if (_mixinTypeEnabled.ContainsKey(mixinType))
            _mixinTypeEnabled[mixinType] = false;
        PrefabSource.RaiseEnvironmentChanged();
    }

    /// <summary>
    /// Gets all enabled mixin types registered for the specified <paramref name="viewModelType"/>.
    /// </summary>
    internal IEnumerable<Type> GetEnabledMixinTypes(Type viewModelType) =>
        Mixins.TryGetValue(viewModelType, out var mixinTypes) ? mixinTypes.Where(IsMixinEnabled) : [];

    internal IEnumerable<Type> GetAllEnabledMixinTypes() => Mixins.Values.SelectMany(x => x).Where(IsMixinEnabled).Distinct();

    /// <summary>
    /// Gets all <see cref="ViewModel"/> target types that have registered mixins.
    /// </summary>
    internal IEnumerable<Type> GetMixinTargetTypes() => Mixins.Keys;

    private bool IsMixinEnabled(Type mixinType) => _mixinTypeEnabled.TryGetValue(mixinType, out var enabled) && enabled;

    /// <summary>
    /// Registers a view model mixin type.
    /// </summary>
    /// <param name="mixinType">The mixin type to register. Must derive from <see cref="BaseViewModelMixin{TViewModel}"/>.</param>
    /// <param name="refreshMethodName">Optional name of a custom refresh method to invoke when the target view model refreshes.</param>
    /// <param name="handleDerived">If <see langword="true"/>, registers the mixin for all types derived from the target view model type.</param>
    /// <remarks>
    /// Diagnostics and validation failures are written to the trace log to assist developers while preventing runtime interruptions.
    /// </remarks>
    public void RegisterViewModelMixin(Type mixinType, string? refreshMethodName = null, bool handleDerived = false)
    {
        var viewModelType = GetViewModelType(mixinType);
        if (viewModelType is null)
        {
            MessageUtils.Fail($"Failed to find base type for mixin {mixinType}, should be specialized as T of ViewModelMixin<T>!");
            return;
        }

        // Target view models must be concrete unless handleDerived is true, as abstract view models are never instantiated directly.
        if (viewModelType.IsAbstract && !handleDerived)
        {
            Trace.TraceWarning("UIExtenderEx: {0}: mixin {1} is not registered: {2} is abstract, so no instance is exactly that type and the mixin would never run. Pass handleDerived: true to reach the types derived from it.",
                _moduleName, mixinType.FullName, viewModelType.FullName);
            return;
        }

        var targets = handleDerived
            ? AccessTools2.AllTypes().Where(t => viewModelType.IsAssignableFrom(t)).ToList()
            : [viewModelType];

        // Report member collisions prior to registration to compare against previously registered types.
        ReportCollisions(mixinType, targets);
        RegisterOverrides(mixinType, targets);

        foreach (var target in targets)
        {
            Mixins.GetOrAdd(target, _ => []).Add(mixinType);
            _mixinTypeEnabled[mixinType] = false;
            ViewModelWithMixinPatch.Patch(UIExtender.Harmony, target, refreshMethodName);
        }
    }

    /// <summary>
    /// Inspects and logs collisions where mixin members shadow host view model members or collide with other mixin members.
    /// </summary>
    private void ReportCollisions(Type mixinType, IReadOnlyList<Type> targets)
    {
        var properties = CollectMixinProperties(mixinType);
        var methods = CollectMixinMethods(mixinType);
        if (properties.Count == 0 && methods.Count == 0)
            return;

        var reportedHostMembers = new HashSet<MemberInfo>();
        var reportedMixinNames = new HashSet<(Type Other, string Name)>();
        foreach (var target in targets)
        {
            foreach (var property in properties)
            {
                if (FindTableProperty(target, property.Name) is { } hostProperty && reportedHostMembers.Add(hostProperty))
                {
                    Trace.TraceWarning("UIExtenderEx: {0}: property '{1}' of mixin {2} replaces {3}.{1} in the binding table of {4}; bindings of that name read the mixin's.",
                        _moduleName, property.Name, mixinType.FullName, hostProperty.DeclaringType?.FullName, target.FullName);
                }
            }
            foreach (var method in methods)
            {
                if (FindCommandMethod(target, method.Name) is { } hostMethod && reportedHostMembers.Add(hostMethod))
                {
                    Trace.TraceWarning("UIExtenderEx: {0}: command '{1}' of mixin {2} replaces {3}.{1} for the prefabs of {4}; the game's own calls to the method still run the ViewModel's.",
                        _moduleName, method.Name, mixinType.FullName, hostMethod.DeclaringType?.FullName, target.FullName);
                }
            }

            foreach (var runtime in UIExtender.GetAllRuntimes())
            {
                if (!runtime.ViewModelComponent.Mixins.TryGetValue(target, out var others))
                    continue;
                foreach (var other in others.ToArray())
                {
                    if (other == mixinType)
                        continue;
                    var names = CollectMixinProperties(other).Select(x => x.Name).Intersect(properties.Select(x => x.Name))
                        .Concat(CollectMixinMethods(other).Select(x => x.Name).Intersect(methods.Select(x => x.Name)));
                    foreach (var name in names)
                    {
                        if (reportedMixinNames.Add((other, name)))
                        {
                            Trace.TraceWarning("UIExtenderEx: '{0}' is added to {1} by mixin {2} of {3} and by mixin {4} of {5}; bindings reach the one registered last, {4}.",
                                name, target.FullName, other.FullName, runtime.ModuleName, mixinType.FullName, _moduleName);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Attempts to retrieve the active override method for the specified mixin type and patched view model method.
    /// </summary>
    internal bool TryGetOverride(Type mixinType, MethodBase method, out MethodInfo overrideMethod)
    {
        overrideMethod = null!;
        return IsMixinEnabled(mixinType)
               && _mixinOverrides.TryGetValue(mixinType, out var overrides)
               && overrides.TryGetValue(method, out overrideMethod!);
    }

    /// <summary>
    /// Discovers and registers <see cref="BUTRViewModelOverrideAttribute"/> interceptors on the mixin type and applies Harmony patches.
    /// </summary>
    private void RegisterOverrides(Type mixinType, IReadOnlyList<Type> targets)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var method in mixinType.GetMethods(all))
        {
            if (method.GetCustomAttribute<BUTRViewModelOverrideAttribute>() is not { } attribute)
                continue;
            if (WhyOverrideIsMalformed(method) is { } malformed)
            {
                Trace.TraceWarning("UIExtenderEx: {0}: override {1}.{2} is not registered: {3}.", _moduleName, mixinType.FullName, method.Name, malformed);
                continue;
            }

            var parameters = method.GetParameters();
            var hostParameters = parameters.Take(parameters.Length - 1).Select(x => x.ParameterType).ToArray();
            var reportedMissing = false;
            foreach (var target in targets)
            {
                var hostMethod = AccessTools.Method(target, attribute.MethodName, hostParameters);
                if (hostMethod is null || hostMethod.IsStatic || hostMethod.ReturnType != typeof(void))
                {
                    if (!reportedMissing)
                    {
                        reportedMissing = true;
                        Trace.TraceWarning("UIExtenderEx: {0}: override {1}.{2} is not registered for {3}: it has no instance method {4}({5}) returning void.",
                            _moduleName, mixinType.FullName, method.Name, target.FullName, attribute.MethodName, string.Join(", ", hostParameters.Select(x => x.Name)));
                    }
                    continue;
                }

                // Target the declared member on the defining type so Harmony prefixes receive the exact target method definition.
                var declared = hostMethod.IsDeclaredMember() ? hostMethod : hostMethod.GetDeclaredMember();
                _mixinOverrides.GetOrAdd(mixinType, _ => new())[declared] = method;
                ViewModelOverridePatch.Patch(UIExtender.Harmony, declared);
            }
        }
    }

    /// <summary>
    /// Validates the signature of a <see cref="BUTRViewModelOverrideAttribute"/> method.
    /// Returns an error description if the signature is invalid; otherwise, returns <see langword="null"/>.
    /// </summary>
    internal static string? WhyOverrideIsMalformed(MethodInfo method)
    {
        if (method.IsStatic)
            return "it is static";
        if (method.ReturnType != typeof(void))
            return "only methods returning void can be taken over";
        var parameters = method.GetParameters();
        if (parameters.Any(x => x.ParameterType.IsByRef))
            return "methods with ref or out parameters cannot be taken over";
        if (parameters.Length == 0 || !typeof(Delegate).IsAssignableFrom(parameters[parameters.Length - 1].ParameterType))
            return "its last parameter has to be the original, a delegate";
        var invoke = parameters[parameters.Length - 1].ParameterType.GetMethod("Invoke")!;
        var delegateParameters = invoke.GetParameters().Select(x => x.ParameterType).ToArray();
        var hostParameters = parameters.Take(parameters.Length - 1).Select(x => x.ParameterType).ToArray();
        if (invoke.ReturnType != typeof(void) || !delegateParameters.SequenceEqual(hostParameters))
            return $"its original has to be a delegate taking ({string.Join(", ", hostParameters.Select(x => x.Name))}) and returning void";
        return null;
    }

    /// <summary>
    /// Locates an existing property on the target <see cref="ViewModel"/> matching the specified name.
    /// </summary>
    private static PropertyInfo? FindTableProperty(Type viewModelType, string name) =>
        viewModelType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(x => x.Name == name && x.GetIndexParameters().Length == 0);

    /// <summary>
    /// Locates an existing command method on the target <see cref="ViewModel"/> hierarchy matching the specified name.
    /// </summary>
    private static MethodInfo? FindCommandMethod(Type viewModelType, string name)
    {
        const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (var type = viewModelType; type is not null && type != typeof(object); type = type.BaseType)
        {
            if (type.GetMethods(declared).FirstOrDefault(x => x.Name == name) is { } method)
                return method;
        }
        return null;
    }

    /// <summary>Collects public properties decorated with <see cref="DataSourceProperty"/> on the mixin type.</summary>
    private static List<PropertyInfo> CollectMixinProperties(Type mixinType) =>
        [.. mixinType.GetProperties().Where(p => p.CustomAttributes.Any(a => a.AttributeType == typeof(DataSourceProperty)))];

    /// <summary>Collects public command methods decorated with <see cref="DataSourceMethodAttribute"/> on the mixin type.</summary>
    private static List<MethodInfo> CollectMixinMethods(Type mixinType) =>
        [.. mixinType.GetMethods().Where(p => p.CustomAttributes.Any(a => a.AttributeType == typeof(DataSourceMethodAttribute)))];

    /// <summary>
    /// Deregisters all mixin definitions registered by this module.
    /// Host view model patches remain active but execute without mixin hooks.
    /// </summary>
    public void Deregister()
    {
        Mixins.Clear();
        //MixinInstanceCache.Clear();
        //MixinInstanceRefreshFromConstructorCache.Clear();
        _mixinTypeEnabled.Clear();
        _mixinTypePropertyCache.Clear();
        _mixinTypeMethodCache.Clear();
        _mixinOverrides.Clear();

        // Retain retired instance to allow active view models to finalize attached mixins gracefully.
        lock (RetiredLock)
            Volatile.Write(ref _retired, [.. _retired, this]);

        // Invalidate environment so cached compiled prefabs discard bindings referring to removed mixin members.
        PrefabSource.RaiseEnvironmentChanged();
    }

    /// <summary>
    /// Instantiates and binds registered mixins to the provided <see cref="ViewModel"/> instance.
    /// </summary>
    /// <param name="instance">The target view model instance being extended.</param>
    public void InitializeMixinsForVMInstance(ViewModel instance)
    {
        // Ignore view model types without registered mixins.
        var type = instance.GetType();
        if (!Mixins.TryGetValue(type, out var mixinTypes))
            return;

        var mixins = MixinInstanceCache.GetOrAdd(instance, _ => []);

        var newMixins = mixinTypes
            .Where(mixinType => _mixinTypeEnabled.TryGetValue(mixinType, out var enabled) && enabled)
            .Where(mixinType => mixins.All(mixin => mixin.GetType() != mixinType))
            .Select(mixinType => Activator.CreateInstance(mixinType, instance) as IViewModelMixin)
            .Where(mixin => mixin is not null)
            .Cast<IViewModelMixin>()
            .ToList();

        mixins.AddRange(newMixins);

        // Batch property and method additions into a single registration scope to avoid repeated dictionary reallocations.
        using (var registration = instance.BeginRegistration())
        {
            foreach (var viewModelMixin in newMixins)
            {
                var properties = _mixinTypePropertyCache.GetOrAdd(viewModelMixin.GetType(), CollectMixinProperties);
                foreach (var property in properties)
                {
                    registration.AddProperty(property.Name, new WrappedPropertyInfo(property, viewModelMixin));
                }

                var methods = _mixinTypeMethodCache.GetOrAdd(viewModelMixin.GetType(), CollectMixinMethods);
                foreach (var method in methods)
                {
                    registration.AddMethod(method.Name, new WrappedMethodInfo(method, viewModelMixin));
                }
            }
        }

        // Subscribe to notifications after construction to prevent premature event handling on partially initialized instances.
        foreach (var viewModelMixin in newMixins)
            (viewModelMixin as IViewModelMixinNotifications)?.Subscribe();
    }

    /// <summary>
    /// Resolves the target <see cref="ViewModel"/> type extended by the specified <paramref name="mixinType"/>.
    /// </summary>
    internal static Type? GetViewModelType(Type mixinType)
    {
        for (var baseType = mixinType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(BaseViewModelMixin<>))
                return baseType.GetGenericArguments()[0];
        }

        Type? viewModelType = null;
        var node = mixinType;
        while (node is not null)
        {
            if (typeof(IViewModelMixin).IsAssignableFrom(node))
            {
                viewModelType = node.GetGenericArguments().FirstOrDefault();
                if (viewModelType is not null)
                {
                    break;
                }
            }

            node = node.BaseType;
        }

        return viewModelType;
    }
}