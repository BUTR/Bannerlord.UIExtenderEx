using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections;
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

    private static readonly AccessTools.FieldRef<IDictionary>? CachedViewModelProperties =
        AccessTools2.StaticFieldRefAccess<IDictionary>(typeof(ViewModel), "_cachedViewModelProperties");

    public delegate object DataSourceTypeBindingPropertiesCollectionCtorDelegate(Dictionary<string, PropertyInfo> properties, Dictionary<string, MethodInfo> methods);
    public static readonly DataSourceTypeBindingPropertiesCollectionCtorDelegate? DataSourceTypeBindingPropertiesCollectionCtor =
        AccessTools2.GetDeclaredConstructorDelegate<DataSourceTypeBindingPropertiesCollectionCtorDelegate>(NestedType, [typeof(Dictionary<string, PropertyInfo>), typeof(Dictionary<string, MethodInfo>)
        ]);

    public static void AddProperty(this ViewModel viewModel, string name, PropertyInfo propertyInfo)
    {
        if (!GetOrCreateIndividualStorage(viewModel, out var propDict, out var _))
            return;

        propDict[name] = propertyInfo;
    }

    public static void AddMethod(this ViewModel viewModel, string name, MethodInfo methodInfo)
    {
        if (!GetOrCreateIndividualStorage(viewModel, out var _, out var methodDict))
            return;

        methodDict[name] = methodInfo;
    }

    public static IReadOnlyCollection<PropertyInfo> GetViewModelProperties(this ViewModel viewModel)
    {
        if (PropertiesAndMethods is null || CachedViewModelProperties is null || DataSourceTypeBindingPropertiesCollectionCtor is null || GetProperties is null || GetMethods is null)
            return Array.Empty<PropertyInfo>();

        if (PropertiesAndMethods(viewModel) is not { } storage)
            return Array.Empty<PropertyInfo>();

        var properties = GetProperties(storage);
        return properties.Values;
    }

    public static IReadOnlyCollection<MethodInfo> GetViewModelMethods(this ViewModel viewModel)
    {
        if (PropertiesAndMethods is null || CachedViewModelProperties is null || DataSourceTypeBindingPropertiesCollectionCtor is null || GetProperties is null || GetMethods is null)
            return Array.Empty<MethodInfo>();

        if (PropertiesAndMethods(viewModel) is not { } storage)
            return Array.Empty<MethodInfo>();

        var methods = GetMethods(storage);
        return methods.Values;
    }

    private static bool GetOrCreateIndividualStorage(ViewModel viewModel, [NotNullWhen(true)] out Dictionary<string, PropertyInfo>? propDict, [NotNullWhen(true)] out Dictionary<string, MethodInfo>? methodDict)
    {
        propDict = null;
        methodDict = null;

        if (PropertiesAndMethods is null || CachedViewModelProperties is null || DataSourceTypeBindingPropertiesCollectionCtor is null || GetProperties is null || GetMethods is null)
            return false;

        if (PropertiesAndMethods(viewModel) is not { } storage || CachedViewModelProperties() is not { } staticStorageDict)
            return false;

        var type = viewModel.GetType();
        if (!staticStorageDict.Contains(type) || staticStorageDict[type] is not { } staticStorage)
            return false;

        if ((propDict = GetProperties(storage)) is null || (methodDict = GetMethods(storage)) is null)
            return false;

        // TW caches the properties, since we modify each VM individually, we need to copy them
        if (ReferenceEquals(storage, staticStorage))
            PropertiesAndMethods(viewModel) = DataSourceTypeBindingPropertiesCollectionCtor(propDict = new(propDict), methodDict = new(methodDict));

        return true;
    }
}