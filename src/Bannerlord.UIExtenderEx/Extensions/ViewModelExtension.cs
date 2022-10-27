using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Extensions
{
    internal static class ViewModelExtension
    {
        private static readonly AccessTools.FieldRef<object, Dictionary<string, PropertyInfo>>? PropertyInfosField =
            AccessTools2.FieldRefAccess<Dictionary<string, PropertyInfo>>("TaleWorlds.Library.ViewModel:_propertyInfos");

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
        
        public static void AddProperty(this ViewModel viewModel, string name, PropertyInfo propertyInfo)
        {
            if (PropertyInfosField?.Invoke(viewModel) is { } dict && !dict.ContainsKey(name))
            {
                dict.Add(name, propertyInfo);
            }

            if (PropertiesAndMethods?.Invoke(viewModel) is { } storage)
            {
                if (GetProperties?.Invoke(storage) is { } propDict/* && CachedViewModelProperties is not null*/)
                {
                    // TW caches the properties, since we modify each VM individually, we need to copy them
                    //var staticStorage = CachedViewModelProperties() is { } dict2 && dict2.Contains(type) ? dict2[type] : null;
                    //var staticPropsDict = staticStorage is not null ? GetProperties(staticStorage) : null;
                    //if (propDict == staticPropsDict)
                    //    propDict = new(propDict);
                    
                    propDict[name] = propertyInfo;
                }
            }
        }

        public static void AddMethod(this ViewModel viewModel, string name, MethodInfo methodInfo)
        {
            if (PropertiesAndMethods?.Invoke(viewModel) is { } storage)
            {
                if (GetMethods?.Invoke(storage) is { } methodDict/* && CachedViewModelProperties is not null*/)
                {
                    // TW caches the methods, since we modify each VM individually, we need to copy them
                    //var type = viewModel.GetType();
                    //var staticStorage = CachedViewModelProperties() is { } dict2 && dict2.Contains(type) ? dict2[type] : null;
                    //var staticMethodDict = staticStorage is not null ? GetMethods(staticStorage) : null;
                    //if (methodDict == staticMethodDict)
                    //    methodDict = new(methodDict);
                    
                    methodDict[name] = methodInfo;
                }
            }
        }
    }
}