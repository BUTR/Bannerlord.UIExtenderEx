using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Debug = System.Diagnostics.Debug;

namespace UIExtenderLibModule
{
    public static class Utils
    {
        public static void CompatiblityAssert(bool condition, string text = "no description")
        {
            Debug.Assert(condition, $"Bannerlord compatibility failure: {text}.");
        }

        public static void UserError(string text, params object[] args)
        {
            InformationManager.DisplayMessage(new InformationMessage("UIExtender: " + String.Format(text, args), Colors.Red));
        }
    }
    
    public static class IDictionaryExtensions
    {
        public static T Get<K, T>(this IDictionary<K, T> o, K key, Func<T> def)
        {
            T value;
            if (o.TryGetValue(key, out value))
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
    
    public static class ReflectionHelpers
    {
        public static T PrivateValue<T>(this object o, string fieldName)
        {
            var field = o.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Utils.CompatiblityAssert(field != null, $"private value getter on {o}.{fieldName}");
            return (T) field.GetValue(o);
        }

        public static void PrivateValueSet<T>(this object o, string fieldName, T value)
        {
            var field = o.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Utils.CompatiblityAssert(field != null, $"private value setter on {o}.{fieldName}");
            field.SetValue(o, value);
        }
    }
}