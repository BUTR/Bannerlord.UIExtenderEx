﻿using Bannerlord.BUTR.Shared.Utils;

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Extensions;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.Utils;
using Bannerlord.UIExtenderEx.ViewModels;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Components;

/// <summary>
/// Component that deals with extended VM generation and runtime support
/// </summary>
internal class ViewModelComponent
{
    private readonly string _moduleName;
    private readonly Harmony _harmony;

    /// <summary>
    /// List of registered mixin types
    /// </summary>
    public readonly ConcurrentDictionary<Type, List<Type>> Mixins = new();

    /// <summary>
    /// Cache of mixin instances. Key is generated by `mixinCacheKey`. Instances are removed when original view model is deallocated
    /// </summary>
    internal readonly ConditionalWeakTable<ViewModel, List<IViewModelMixin>> MixinInstanceCache = new();

    internal readonly ConditionalWeakTable<ViewModel, List<string>> MixinInstanceRefreshFromConstructorCache = new();

    private readonly ConcurrentDictionary<Type, bool> _mixinTypeEnabled = new();
    private readonly ConcurrentDictionary<Type, List<PropertyInfo>> _mixinTypePropertyCache = new();
    private readonly ConcurrentDictionary<Type, List<MethodInfo>> _mixinTypeMethodCache = new();

    public ViewModelComponent(string moduleName)
    {
        _moduleName = moduleName;
        _harmony = new Harmony($"bannerlord.uiextender.ex.viewmodels.{_moduleName}");
    }

    /// <summary>
    /// Enables all Mixins.
    /// </summary>
    public void Enable()
    {
        foreach (var mixinType in _mixinTypeEnabled.Keys)
            _mixinTypeEnabled[mixinType] = true;
    }

    /// <summary>
    /// Disables all Mixins.
    /// </summary>
    public void Disable()
    {
        foreach (var mixinType in _mixinTypeEnabled.Keys)
            _mixinTypeEnabled[mixinType] = false;
    }

    /// <summary>
    /// Enables a specific mixin.
    /// </summary>
    /// <param name="mixinType">The Mixin</param>
    public void Enable(Type mixinType)
    {
        if (_mixinTypeEnabled.ContainsKey(mixinType))
            _mixinTypeEnabled[mixinType] = true;
    }

    /// <summary>
    /// Disables a specific mixin.
    /// </summary>
    /// <param name="mixinType">The Mixin</param>
    public void Disable(Type mixinType)
    {
        if (_mixinTypeEnabled.ContainsKey(mixinType))
            _mixinTypeEnabled[mixinType] = false;
    }

    /// <summary>
    /// Register mixin type.
    /// </summary>
    /// <param name="mixinType">mixin type, should be a subclass of <see cref="BaseViewModelMixin{TViewModel}"/> where
    /// the type parameter specifies the view model to extend</param>
    /// <param name="refreshMethodName"></param>
    /// <param name="handleDerived"></param>
    public void RegisterViewModelMixin(Type mixinType, string? refreshMethodName = null, bool handleDerived = false)
    {
        void Patch(Type viewModelType_)
        {
            Mixins.GetOrAdd(viewModelType_, _ => []).Add(mixinType);
            _mixinTypeEnabled[mixinType] = false;
            ViewModelWithMixinPatch.Patch(_harmony, viewModelType_, refreshMethodName);
        }

        var viewModelType = GetViewModelType(mixinType);
        if (viewModelType is null)
        {
            MessageUtils.Fail($"Failed to find base type for mixin {mixinType}, should be specialized as T of ViewModelMixin<T>!");
            return;
        }

        if (handleDerived)
        {
            foreach (var type in AccessTools2.AllTypes().Where(t => viewModelType.IsAssignableFrom(t)))
            {
                Patch(type);
            }
        }
        else
        {
            Patch(viewModelType);
        }
    }

    public void Deregister()
    {
        foreach (var patchedMethod in _harmony.GetPatchedMethods())
        {
            if (patchedMethod is not MethodInfo patchedMethodInfo)
                continue;

            if (Harmony.GetOriginalMethod(patchedMethodInfo) is not { } originalMethodInfo)
                continue;

            _harmony.Unpatch(originalMethodInfo, patchedMethodInfo);
        }

        Mixins.Clear();
        //MixinInstanceCache.Clear();
        //MixinInstanceRefreshFromConstructorCache.Clear();
        _mixinTypeEnabled.Clear();
        _mixinTypePropertyCache.Clear();
        _mixinTypeMethodCache.Clear();
    }

    /// <summary>
    /// Initialize mixin instances for specified view model instance, called in extended VM constructor.
    /// </summary>
    /// <param name="instance">instance of extended VM</param>
    public void InitializeMixinsForVMInstance(ViewModel instance)
    {
        var mixins = MixinInstanceCache.GetOrAdd(instance, _ => new List<IViewModelMixin>());

        var type = instance.GetType();
        if (!Mixins.ContainsKey(type))
            return;

        var newMixins = Mixins[type]
            .Where(mixinType => _mixinTypeEnabled.TryGetValue(mixinType, out var enabled) && enabled)
            .Where(mixinType => mixins.All(mixin => mixin.GetType() != mixinType))
            .Select(mixinType => Activator.CreateInstance(mixinType, instance) as IViewModelMixin)
            .Where(mixin => mixin is not null)
            .Cast<IViewModelMixin>()
            .ToList();

        mixins.AddRange(newMixins);

        foreach (var viewModelMixin in newMixins)
        {
            var properties = _mixinTypePropertyCache.GetOrAdd(viewModelMixin.GetType(), static x => x.GetProperties().Where(p => p.CustomAttributes.Any(a => a.AttributeType == typeof(DataSourceProperty))).ToList());
            foreach (var property in properties)
            {
                instance.AddProperty(property.Name, new WrappedPropertyInfo(property, viewModelMixin));
            }

            var methods = _mixinTypeMethodCache.GetOrAdd(viewModelMixin.GetType(), static x => x.GetMethods().Where(p => p.CustomAttributes.Any(a => a.AttributeType == typeof(DataSourceMethodAttribute))).ToList());
            foreach (var method in methods)
            {
                instance.AddMethod(method.Name, new WrappedMethodInfo(method, viewModelMixin));
            }
        }
    }

    private static Type? GetViewModelType(Type mixinType)
    {
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