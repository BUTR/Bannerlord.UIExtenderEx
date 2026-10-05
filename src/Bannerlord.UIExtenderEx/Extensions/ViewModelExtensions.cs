using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Extensions;

internal static class ViewModelExtensions
{
    private static readonly string NestedType = "TaleWorlds.Library.ViewModel+DataSourceTypeBindingPropertiesCollection";

    private static readonly AccessTools.FieldRef<ViewModel, object>? PropertiesAndMethods =
        AccessTools2.FieldRefAccess<ViewModel, object>("_propertiesAndMethods");

    private delegate Dictionary<string, PropertyInfo> GetPropertiesDelegate(object instance);
    private static readonly GetPropertiesDelegate? GetProperties =
        AccessTools2.GetDeclaredPropertyGetterDelegate<GetPropertiesDelegate>($"{NestedType}:Properties");

    private delegate Dictionary<string, MethodInfo> GetMethodsDelegate(object instance);
    private static readonly GetMethodsDelegate? GetMethods =
        AccessTools2.GetDeclaredPropertyGetterDelegate<GetMethodsDelegate>($"{NestedType}:Methods");

    public delegate object DataSourceTypeBindingPropertiesCollectionCtorDelegate(Dictionary<string, PropertyInfo> properties, Dictionary<string, MethodInfo> methods);
    public static readonly DataSourceTypeBindingPropertiesCollectionCtorDelegate? DataSourceTypeBindingPropertiesCollectionCtor =
        AccessTools2.GetDeclaredConstructorDelegate<DataSourceTypeBindingPropertiesCollectionCtorDelegate>(NestedType, [typeof(Dictionary<string, PropertyInfo>),
            typeof(Dictionary<string, MethodInfo>)
        ]);

    /// <summary>
    /// Determines whether the engine's internal ViewModel binding table reflection members were successfully resolved.
    /// </summary>
    private static bool TableIsReachable =>
        PropertiesAndMethods is not null && GetProperties is not null && GetMethods is not null && DataSourceTypeBindingPropertiesCollectionCtor is not null;

    extension(ViewModel viewModel)
    {
        /// <summary>
        /// Registers an individual property on this specific ViewModel instance by cloning and atomically publishing an updated binding table.
        /// <para>
        /// TaleWorlds shares a single static binding table per ViewModel type. To ensure per-instance mixin isolation
        /// without mutating global type tables, registrations allocate an instance-specific copy. For multiple members,
        /// prefer <see cref="BeginRegistration"/> to perform batch publication in a single clone operation.
        /// </para>
        /// </summary>
        public void AddProperty(string name, PropertyInfo propertyInfo)
        {
            using var registration = viewModel.BeginRegistration();
            registration.AddProperty(name, propertyInfo);
        }

        /// <summary>
        /// Registers an individual command method on this specific ViewModel instance by cloning and atomically publishing an updated binding table.
        /// </summary>
        public void AddMethod(string name, MethodInfo methodInfo)
        {
            using var registration = viewModel.BeginRegistration();
            registration.AddMethod(name, methodInfo);
        }

        /// <summary>
        /// Begins a batched binding registration operation, cloning the instance's binding table upon creation and
        /// atomically publishing the updated table upon disposal.
        /// </summary>
        public ViewModelBindingRegistration BeginRegistration() => new(viewModel);

        /// <summary>
        /// Resolves the <see cref="PropertyInfo"/> associated with the specified name in this instance's dynamic binding table,
        /// matching <see cref="ViewModel.GetPropertyValue(string)"/> lookup semantics.
        /// </summary>
        public PropertyInfo? SelectProperty(string name)
        {
            if (PropertiesAndMethods is null || GetProperties is null)
                return null;
            if (PropertiesAndMethods(viewModel) is not { } storage || GetProperties(storage) is not { } properties)
                return null;
            return properties.TryGetValue(name, out var propertyInfo) ? propertyInfo : null;
        }

        /// <summary>
        /// Resolves the <see cref="MethodInfo"/> associated with the specified command name, checking the instance binding
        /// table before walking the inheritance hierarchy (matching <see cref="ViewModel.ExecuteCommand"/> semantics).
        /// </summary>
        public MethodInfo? SelectMethod(string name)
        {
            if (PropertiesAndMethods is not null && GetMethods is not null
                                                 && PropertiesAndMethods(viewModel) is { } storage && GetMethods(storage) is { } methods
                                                 && methods.TryGetValue(name, out var registered))
            {
                return registered;
            }

            for (var type = viewModel.GetType(); type is not null; type = type.BaseType)
            {
                if (type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } methodInfo)
                    return methodInfo;
            }
            return null;
        }

        public IReadOnlyCollection<PropertyInfo> GetViewModelProperties()
        {
            if (!TableIsReachable || PropertiesAndMethods!(viewModel) is not { } storage || GetProperties!(storage) is not { } properties)
                return [];

            return properties.Values;
        }

        public IReadOnlyCollection<MethodInfo> GetViewModelMethods()
        {
            if (!TableIsReachable || PropertiesAndMethods!(viewModel) is not { } storage || GetMethods!(storage) is not { } methods)
                return [];

            return methods.Values;
        }
    }

    /// <summary>
    /// Represents an active batch registration scope. New member registrations are accumulated into a local copy
    /// and atomically published upon disposal.
    /// <para>
    /// Batch operations run synchronously on the main UI thread during ViewModel initialization.
    /// </para>
    /// </summary>
    public readonly struct ViewModelBindingRegistration : IDisposable
    {
        private readonly ViewModel? _viewModel;
        private readonly Dictionary<string, PropertyInfo>? _properties;
        private readonly Dictionary<string, MethodInfo>? _methods;

        internal ViewModelBindingRegistration(ViewModel viewModel)
        {
            _viewModel = null;
            _properties = null;
            _methods = null;

            if (!TryCopyCurrent(viewModel, out var properties, out var methods))
                return;

            _viewModel = viewModel;
            _properties = properties;
            _methods = methods;
        }

        public void AddProperty(string name, PropertyInfo propertyInfo)
        {
            _properties?[name] = propertyInfo;
        }

        public void AddMethod(string name, MethodInfo methodInfo)
        {
            _methods?[name] = methodInfo;
        }

        /// <summary>
        /// Atomically publishes the completed binding table to the target ViewModel instance.
        /// </summary>
        public void Dispose()
        {
            if (_viewModel is null || _properties is null || _methods is null)
                return;
            PropertiesAndMethods!(_viewModel) = DataSourceTypeBindingPropertiesCollectionCtor!(_properties, _methods);
        }

        private static bool TryCopyCurrent(
            ViewModel viewModel,
            [NotNullWhen(true)] out Dictionary<string, PropertyInfo>? properties,
            [NotNullWhen(true)] out Dictionary<string, MethodInfo>? methods)
        {
            properties = null;
            methods = null;

            if (!TableIsReachable || PropertiesAndMethods!(viewModel) is not { } storage)
                return false;
            if (GetProperties!(storage) is not { } currentProperties || GetMethods!(storage) is not { } currentMethods)
                return false;

            // TaleWorlds shares one static table per ViewModel type; clone current mappings to preserve immutability
            properties = new(currentProperties);
            methods = new(currentMethods);
            return true;
        }
    }
}