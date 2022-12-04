using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Extensions
{
    internal static class ViewModelExtension
    {
        private static readonly AccessTools.FieldRef<object, object>? PropertiesAndMethods =
            AccessTools2.FieldRefAccess<object>("TaleWorlds.Library.ViewModel:_propertiesAndMethods");

        private delegate Dictionary<string, PropertyInfo> GetPropertiesDelegate(object instance);
        private static readonly GetPropertiesDelegate? GetProperties =
            AccessTools2.GetDeclaredPropertyGetterDelegate<GetPropertiesDelegate>("TaleWorlds.Library.ViewModel+DataSourceTypeBindingPropertiesCollection:Properties");

        private delegate Dictionary<string, MethodInfo> GetMethodsDelegate(object instance);
        private static readonly GetMethodsDelegate? GetMethods =
            AccessTools2.GetDeclaredPropertyGetterDelegate<GetMethodsDelegate>("TaleWorlds.Library.ViewModel+DataSourceTypeBindingPropertiesCollection:Methods");

        private static readonly AccessTools.FieldRef<IDictionary>? CachedViewModelProperties =
            AccessTools2.StaticFieldRefAccess<IDictionary>("TaleWorlds.Library.ViewModel:_cachedViewModelProperties");

        private delegate object DataSourceTypeBindingPropertiesCollectionCtorDelegate(Dictionary<string, PropertyInfo> properties, Dictionary<string, MethodInfo> methods);
        private static readonly DataSourceTypeBindingPropertiesCollectionCtorDelegate? DataSourceTypeBindingPropertiesCollectionCtor =
            AccessTools2.GetDeclaredConstructorDelegate<DataSourceTypeBindingPropertiesCollectionCtorDelegate>(
                "TaleWorlds.Library.ViewModel+DataSourceTypeBindingPropertiesCollection", new []{ typeof(Dictionary<string, PropertyInfo>), typeof(Dictionary<string, MethodInfo>) });

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

        private static bool GetOrCreateIndividualStorage(ViewModel viewModel, [NotNullWhen(true)] out Dictionary<string, PropertyInfo>? propDict,  [NotNullWhen(true)] out Dictionary<string, MethodInfo>? methodDict)
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
}