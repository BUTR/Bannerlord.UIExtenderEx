using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using Debug = System.Diagnostics.Debug;
using Path = System.IO.Path;

namespace UIExtenderLib
{
    public static class Utils
    {
        /// <summary>
        /// Non-critical failure, should only crash on debug
        /// </summary>
        /// <param name="text"></param>
        public static void SoftFail(string text)
        {
            if (Debugger.IsAttached)
            {
                Debug.Fail(text);
            }
        }

        /// <summary>
        /// Non-critical assert, should only crash on debug, otherwise return bool of the condition
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool SoftAssert(bool condition, string text)
        {
            if (Debugger.IsAttached)
            {
                Debug.Assert(condition, text);
            }

            return condition;
        }
        
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
            InformationManager.DisplayMessage(new InformationMessage("UIExtender: " + String.Format(text, args), Colors.Red));
        }

        /// <summary>
        /// Display warning message to the end user
        /// </summary>
        /// <param name="text"></param>
        /// <param name="args"></param>
        public static void UserWarning(string text, params object[] args)
        {
            InformationManager.DisplayMessage(new InformationMessage("UIExtender: " + String.Format(text, args), Colors.Yellow));
        }

        public static string GeneratedDllDirectory()
        {
            var path = Path.Combine(Utilities.GetBasePath(), "bin", "UIExtenderLib_gen_dll");
            Directory.CreateDirectory(path);
            return path;
        }
    }
    
    public static class IDictionaryExtensions
    {
        /// <summary>
        /// Extension method on IDictionary<K, V> that will either return value for key (if exist),
        /// or call `Func<V> def` to create it, store and then return
        /// </summary>
        /// <param name="o"></param>
        /// <param name="key">key to use</param>
        /// <param name="def">function that returns value if not exists</param>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <returns></returns>
        public static V Get<K, V>(this IDictionary<K, V> o, K key, Func<V> def)
        {
            V value;
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