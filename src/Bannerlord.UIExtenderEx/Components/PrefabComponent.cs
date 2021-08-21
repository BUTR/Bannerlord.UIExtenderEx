using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

using Bannerlord.UIExtenderEx.Prefabs2;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Components
{
    /// <summary>
    /// Component that deals with Gauntlet prefab XML files
    /// </summary>
    internal partial class PrefabComponent
    {
        private static readonly AccessTools.FieldRef<WidgetFactory, IDictionary>? GetCustomTypes = AccessTools2.FieldRefAccess<WidgetFactory, IDictionary>("_customTypes");

        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Keeping it for consistency>")]
        private readonly string _moduleName;

        /// <summary>
        /// Registered movie patches
        /// </summary>
        private readonly ConcurrentDictionary<string, List<Action<XmlDocument>>> _moviePatches = new();

        public bool Enabled { get; private set; }

        /// <summary>
        /// When set to true, patches that are loaded from file (<see cref="PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute"/>)
        /// will be reloaded every time their target view is reloaded.<br/>
        /// This is slower, so should only be enabled while in a development environment.
        /// </summary>
        public bool LiveUIDebuggingEnabled { get; set; }

        public PrefabComponent(string moduleName)
        {
            _moduleName = moduleName;
        }

        public void Enable()
        {
            Enabled = true;
            ForceReloadMovies();
        }
        public void Disable()
        {
            Enabled = false;
            ForceReloadMovies();
        }

        /// <summary>
        /// Register general XmlDocument patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="patcher"></param>
        public void RegisterPatch(string movie, Action<XmlDocument> patcher)
        {
            if (string.IsNullOrEmpty(movie))
            {
                Utils.Fail("Invalid movie name!");
                return;
            }

            _moviePatches.GetOrAdd(movie, _ => new List<Action<XmlDocument>>()).Add(patcher);
        }

        /// <summary>
        /// Register general XmlDocument patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="patcher"></param>
        public void RegisterPatch(string movie, Action<XmlNode> patcher)
        {
            //RegisterPatch(movie, (XmlDocument node) => patcher(node));
            if (string.IsNullOrEmpty(movie))
            {
                Utils.Fail("Invalid movie name!");
                return;
            }

            _moviePatches.GetOrAdd(movie, _ => new List<Action<XmlDocument>>()).Add(patcher);
        }

        /// <summary>
        /// Register patch operating at node specified by XPath
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patcher"></param>
        public void RegisterPatch(string movie, string? xpath, Action<XmlNode> patcher) => RegisterPatch(movie, node =>
        {
            var node2 = node.SelectSingleNode(xpath ?? string.Empty);
            if (node2 is null)
            {
                Utils.DisplayUserError($"Failed to apply extension to {movie}: node at {xpath} not found.");
                return;
            }

            patcher(node2);
        });

        /// <summary>
        /// Make WidgetFactory reload Movies that were extended by _moviePatches.
        /// WidgetFactory loads Movies during SandBox module loading phase, which occurs even before
        /// our module gets loaded, hence once we get control we need to force it to reload XMLs that
        /// are getting patched by extensions.
        /// </summary>
        private void ForceReloadMovies()
        {
            foreach (var movie in _moviePatches.Keys)
            {
                var moviePath = PathForMovie(movie);
                if (moviePath is not null)
                {
                    // pre e1.5.4
                    // get internal dict of loaded Widgets
                    if (GetCustomTypes is not null)
                    {
                        var dict = GetCustomTypes(UIResourceManager.WidgetFactory);
                        Utils.Assert(dict.Contains(movie), $"Movie {movie} to be patched was not found in the WidgetFactory._customTypes!");
                        // remove widget from previously loaded Widgets
                        dict.Remove(movie);

                        // re-add it, forcing Factory to call now-patched `LoadFrom` method
                        UIResourceManager.WidgetFactory.AddCustomType(movie, moviePath);
                    }
                }
            }
        }

        /// <summary>
        /// Fixes issue where game will crash if injected patch contains comments.<br/>
        /// Returns false when <paramref name="node"/> is a comment, or is null.
        /// </summary>
        private static bool TryRemoveComments(XmlNode? node)
        {
            if (string.Equals(node?.Name, "#comment"))
            {
                return false;
            }

            if (node?.SelectNodes("//comment()") is not { } commentNodes)
            {
                return false;
            }

            foreach (XmlNode xmlNode in commentNodes)
            {
                xmlNode.ParentNode!.RemoveChild(xmlNode);
            }

            return true;
        }

        /// <summary>
        /// Get path for movie from WidgetFactory
        /// </summary>
        /// <param name="movie"></param>
        private static string? PathForMovie(string movie)
        {
            // TODO: figure out a method more prone to game updates
            var prefabNamesMethod = AccessTools.DeclaredMethod(typeof(WidgetFactory), "GetPrefabNamesAndPathsFromCurrentPath");
            if (prefabNamesMethod?.Invoke(UIResourceManager.WidgetFactory, Array.Empty<object>()) is Dictionary<string, string> paths)
            {
                return paths[movie];
            }
            else
            {
                Utils.DisplayUserError("UIExtenderEx could not find WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath!");
                return null;
            }
        }

        /// <summary>
        /// Apply patches to movie (if any is registered)
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="document"></param>
        public void ProcessMovieIfNeeded(string movie, XmlDocument document)
        {
            if (!_moviePatches.TryGetValue(movie, out var patches))
                return;

            foreach (var patch in patches)
            {
                patch(document);
            }
        }
    }
}