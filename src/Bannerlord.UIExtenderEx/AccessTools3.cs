using Bannerlord.BUTR.Shared.Helpers;

using HarmonyLib;

using System;
using System.Reflection;

namespace Bannerlord.UIExtenderEx
{
    internal static class AccessTools3
    {
        /// <summary>Creates an instance field reference delegate for a private type</summary>
        /// <typeparam name="TF">The type of the field</typeparam>
        /// <param name="type">The class/type</param>
        /// <param name="fieldName">The name of the field</param>
        /// <returns>A read and writable <see cref="T:HarmonyLib.AccessTools.FieldRef`2" /> delegate</returns>
        public static AccessTools.FieldRef<object, TF>? FieldRefAccess<TF>(Type type, string fieldName)
        {
            var field = AccessTools.Field(type, fieldName);
            return field is null ? null : AccessTools.FieldRefAccess<object, TF>(field);
        }

        /// <summary>Creates an instance field reference</summary>
        /// <typeparam name="T">The class the field is defined in</typeparam>
        /// <typeparam name="TF">The type of the field</typeparam>
        /// <param name="fieldName">The name of the field</param>
        /// <returns>A read and writable field reference delegate</returns>
        public static AccessTools.FieldRef<T, TF>? FieldRefAccess<T, TF>(string fieldName)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic);
            return field is null ? null : AccessTools.FieldRefAccess<T, TF>(field);
        }

        public static TDelegate? GetDelegate<TDelegate>(ConstructorInfo? constructorInfo) where TDelegate : Delegate
            => ReflectionHelper.GetDelegate<TDelegate>(constructorInfo);

        /// <summary>Get a delegate for a method described by <paramref name="methodInfo"/>.</summary>
        /// <param name="methodInfo">The method's <see cref="MethodInfo"/>.</param>
        /// <returns>A delegate or <see langword="null"/> when <paramref name="methodInfo"/> is <see langword="null"/>.</returns>
        public static TDelegate? GetDelegate<TDelegate>(MethodInfo? methodInfo) where TDelegate : Delegate => ReflectionHelper.GetDelegate<TDelegate>(methodInfo);

        /// <summary>
        /// Get a delegate for a method named <paramref name="method"/>, declared by <paramref name="type"/> or any of its base types.
        /// </summary>
        /// <param name="type">The type from which to start searching for the method's definition.</param>
        /// <param name="method">The name of the method (case sensitive).</param>
        /// <returns>
        /// A delegate or <see langword="null"/> when <paramref name="type"/> or <paramref name="method"/>
        /// is <see langword="null"/> or when the method cannot be found.
        /// </returns>
        public static TDelegate? GetDelegate<TDelegate>(Type type, string method) where TDelegate : Delegate => GetDelegate<TDelegate>(AccessTools.Method(type, method));
    }
}