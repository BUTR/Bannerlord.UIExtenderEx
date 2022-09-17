using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Extensions
{
    internal static class ViewModelExtension
    {
        private static readonly AccessTools.FieldRef<object, Dictionary<string, PropertyInfo>>? PropertyInfosField =
            AccessTools2.FieldRefAccess<Dictionary<string, PropertyInfo>>("TaleWorlds.Library.ViewModel:_propertyInfos");


        private delegate Dictionary<string, PropertyInfo> GetPropertiesDelegate(object instance);
        private delegate Dictionary<string, MethodInfo> GetMethodsDelegate(object instance);

        private static readonly AccessTools.FieldRef<object, object>? PropertiesAndMethods =
            AccessTools2.FieldRefAccess<object>("TaleWorlds.Library.ViewModel:_propertiesAndMethods");

        private static readonly GetPropertiesDelegate? GetProperties =
            AccessTools2.GetDeclaredPropertyGetterDelegate<GetPropertiesDelegate>("TaleWorlds.Library.ViewModel+DataSourceTypeBindingPropertiesCollection:Properties");
        private static readonly GetMethodsDelegate? GetMethods =
            AccessTools2.GetDeclaredPropertyGetterDelegate<GetMethodsDelegate>("TaleWorlds.Library.ViewModel+DataSourceTypeBindingPropertiesCollection:Methods");

        public static void AddProperty(this ViewModel viewModel, string name, PropertyInfo propertyInfo)
        {
            if (PropertyInfosField?.Invoke(viewModel) is { } dict && !dict.ContainsKey(name))
            {
                dict.Add(name, propertyInfo);
            }

            if (PropertiesAndMethods?.Invoke(viewModel) is { } storage)
            {
                if (GetProperties?.Invoke(storage) is { } propDict)
                {
                    propDict[name] = propertyInfo;
                }
            }
        }

        public static void AddMethod(this ViewModel viewModel, string name, MethodInfo methodInfo)
        {
            if (PropertiesAndMethods?.Invoke(viewModel) is { } storage)
            {
                if (GetMethods?.Invoke(storage) is { } methodDict)
                {
                    methodDict[name] = methodInfo;
                }
            }
        }
    }
}