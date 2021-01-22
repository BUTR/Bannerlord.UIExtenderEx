using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

using Bannerlord.UIExtenderEx.Components;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2.Utilities
{
    internal static class PrefabComponentExtensions
    {
        public static List<Action<XmlDocument>> GetMoviePatches(this PrefabComponent instance, string movieName)
        {
            return _lazyGetMoviePatches.Value(instance, movieName);
        }

        private static readonly Lazy<Func<PrefabComponent, string, List<Action<XmlDocument>>>> _lazyGetMoviePatches = new(() =>
        {
            var fieldInfo = typeof(PrefabComponent).GetField("_moviePatches", BindingFlags.Instance | BindingFlags.NonPublic);
            return (instance, movieName) => ( (ConcurrentDictionary<string, List<Action<XmlDocument>>>) fieldInfo!.GetValue(instance) )[movieName];
        });
    }
}