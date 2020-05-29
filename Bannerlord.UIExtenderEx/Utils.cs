using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.Core;
using TaleWorlds.Library;

using Debug = System.Diagnostics.Debug;

namespace Bannerlord.UIExtenderEx
{
    internal static class Utils
    {
        /// <summary>
        /// Critical runtime compatibility assert. Used when Bannerlord version is not compatible and it
        /// prevents runtime from functioning
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="text"></param>
        public static void CompatAssert(bool condition, string text = "no description")
        {
            Debug.Assert(condition, $"Bannerlord compatibility failure: {text}.");
        }

        /// <summary>
        /// Display error message to the end user
        /// </summary>
        /// <param name="text"></param>
        /// <param name="args"></param>
        public static void DisplayUserError(string text, params object[] args)
        {
            InformationManager.DisplayMessage(new InformationMessage($"UIExtender: {string.Format(text, args)}", Colors.Red));
        }

        /// <summary>
        /// Display warning message to the end user
        /// </summary>
        /// <param name="text"></param>
        /// <param name="args"></param>
        public static void UserWarning(string text, params object[] args)
        {
            InformationManager.DisplayMessage(new InformationMessage($"UIExtender: {string.Format(text, args)}", Colors.Yellow));
        }
    }

    internal static class ViewModelExtension
    {
        private static AccessTools.FieldRef<ViewModel, Dictionary<string, PropertyInfo>> _propertyInfosField =
            AccessTools.FieldRefAccess<ViewModel, Dictionary<string, PropertyInfo>>("_propertyInfos");

        public static void AddProperty(this ViewModel viewModel, string name, PropertyInfo propertyInfo) =>
            _propertyInfosField(viewModel).Add(name, propertyInfo);
    }

    internal static class IDictionaryExtensions
    {
        /// <summary>
        /// Extension method on IDictionary<TKey, TValue> that will either return value for key (if exist),
        /// or call `Func<TValue> def` to create it, store and then return
        /// </summary>
        /// <param name="o"></param>
        /// <param name="key">key to use</param>
        /// <param name="def">function that returns value if not exists</param>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <returns></returns>
        public static TValue Get<TKey, TValue>(this IDictionary<TKey, TValue> o, TKey key, Func<TValue> def)
        {
            if (o.TryGetValue(key, out var value))
            {
                return value;
            }
            else
            {
                value = def();
                o[key] = value;
                return value;
            }
        }
    }
    
    internal static class ReflectionHelpers
    {
        public static T PrivateValue<T>(this object o, string fieldName)
        {
            var field = AccessTools.Field(o.GetType(), fieldName);
            Utils.CompatAssert(field != null, $"private value getter on {o}.{fieldName}");
            return (T) field.GetValue(o);
        }

        public static void PrivateValueSet<T>(this object o, string fieldName, T value)
        {
            var field = o.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Utils.CompatAssert(field != null, $"private value setter on {o}.{fieldName}");
            field.SetValue(o, value);
        }
    }
}