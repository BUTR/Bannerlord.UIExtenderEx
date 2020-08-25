using HarmonyLib;

using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Extensions
{
    internal static class ViewModelExtension
    {
        private static readonly FieldInfo PropertyInfosField = AccessTools.Field(typeof(ViewModel), "_propertyInfos");

        public static void AddProperty(this ViewModel viewModel, string name, PropertyInfo propertyInfo)
        {
            if (PropertyInfosField.GetValue(viewModel) is Dictionary<string, PropertyInfo> dict && !dict.ContainsKey(name))
                dict.Add(name, propertyInfo);
        }
    }
}