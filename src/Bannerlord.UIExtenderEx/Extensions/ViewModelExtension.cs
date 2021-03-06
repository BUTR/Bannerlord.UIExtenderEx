using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Extensions
{
    internal static class ViewModelExtension
    {
        private static readonly AccessTools.FieldRef<ViewModel, Dictionary<string, PropertyInfo>>? PropertyInfosField =
            AccessTools2.FieldRefAccess<ViewModel, Dictionary<string, PropertyInfo>>("_propertyInfos");

        public static void AddProperty(this ViewModel viewModel, string name, PropertyInfo propertyInfo)
        {
            if (PropertyInfosField is not null && PropertyInfosField(viewModel) is { } dict && !dict.ContainsKey(name))
                dict.Add(name, propertyInfo);
        }
    }
}