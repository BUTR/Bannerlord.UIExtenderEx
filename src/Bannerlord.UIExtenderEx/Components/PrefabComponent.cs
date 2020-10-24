using Bannerlord.UIExtenderEx.Prefabs;

using HarmonyLib;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Xml;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Components
{
    /// <summary>
    /// Component that deals with Gauntlet prefab XML files
    /// </summary>
    internal class PrefabComponent
    {
        private static readonly AccessTools.FieldRef<object, IDictionary>? GetCustomTypes =
            AccessTools3.FieldRefAccess<IDictionary>(typeof(WidgetFactory), "_customTypes");
        private static readonly AccessTools.FieldRef<object, IDictionary>? GetCustomTypePaths =
            AccessTools3.FieldRefAccess<IDictionary>(typeof(WidgetFactory), "_customTypePaths");

        private readonly string _moduleName;

        /// <summary>
        /// Registered movie patches
        /// </summary>
        private readonly ConcurrentDictionary<string, List<Action<XmlDocument>>> _moviePatches = new ConcurrentDictionary<string, List<Action<XmlDocument>>>();

        public bool Enabled { get; private set; }

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
            Utils.Assert(!string.IsNullOrEmpty(movie), $"Invalid movie name: {movie}!");

            _moviePatches.GetOrAdd(movie, _ => new List<Action<XmlDocument>>()).Add(patcher);
        }

        /// <summary>
        /// Register patch operating at node specified by XPath
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patcher"></param>
        public void RegisterPatch(string movie, string? xpath, Action<XmlNode> patcher)
        {
            RegisterPatch(movie, document =>
            {
                var node = document.SelectSingleNode(xpath ?? string.Empty);
                if (node == null)
                {
                    Utils.DisplayUserError($"UIExtenderEx failed to apply extension to {movie}: node at {xpath} not found.");
                    return;
                }

                patcher(node);
            });
        }

        /// <summary>
        /// Register snippet insert patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertPatch patch)
        {
            RegisterPatch(movie, xpath, node =>
            {
                var ownerDocument = node is XmlDocument xmlDocument ? xmlDocument : node.OwnerDocument!;

                var extensionNode = patch.GetPrefabExtension().DocumentElement;
                var importedExtensionNode = ownerDocument.ImportNode(extensionNode, true);
                var position = Math.Min(patch.Position, node.ChildNodes.Count - 1);
                position = Math.Max(position, 0);
                if (position >= node.ChildNodes.Count)
                    Utils.Fail($"Invalid position ({position}) for insert (patching in {patch.Id})");

                node.InsertAfter(importedExtensionNode, node.ChildNodes[position]);
            });
        }

        /// <summary>
        /// Register snippet replace patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionReplacePatch patch)
        {
            RegisterPatch(movie, xpath, node =>
            {
                var ownerDocument = node is XmlDocument xmlDocument ? xmlDocument : node.OwnerDocument!;
                if (node.ParentNode == null)
                    return;

                var extensionNode = patch.GetPrefabExtension().DocumentElement;
                var importedExtensionNode = ownerDocument.ImportNode(extensionNode, true);

                node.ParentNode.ReplaceChild(importedExtensionNode, node);
            });
        }

        /// <summary>
        /// Register snippet insert as sibling patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertAsSiblingPatch patch)
        {
            RegisterPatch(movie, xpath, node =>
            {
                var ownerDocument = node is XmlDocument xmlDocument ? xmlDocument : node.OwnerDocument!;
                if (node.ParentNode == null)
                    return;

                var extensionNode = patch.GetPrefabExtension().DocumentElement;
                var importedExtensionNode = ownerDocument.ImportNode(extensionNode, true);

                switch (patch.Type)
                {
                    case PrefabExtensionInsertAsSiblingPatch.InsertType.Append:
                        node.ParentNode.InsertAfter(importedExtensionNode, node);
                        break;

                    case PrefabExtensionInsertAsSiblingPatch.InsertType.Prepend:
                        node.ParentNode.InsertBefore(importedExtensionNode, node);
                        break;
                }
            });
        }

        /// <summary>
        /// Make WidgetFactory reload Movies that were extended by _moviePatches.
        /// WidgetFactory loads Movies during SandBox module loading phase, which occurs even before
        /// our module gets loaded, hence once we get control we need to force it to reload XMLs that
        /// are getting patched by extensions.
        /// </summary>
        private void ForceReloadMovies()
        {
            // TODO: figure out a method more prone to game updates

            foreach (var movie in _moviePatches.Keys)
            {
                var moviePath = PathForMovie(movie);
                if (moviePath != null)
                {
                    // get internal dict of loaded Widgets
                    if (GetCustomTypes != null)
                    {
                        var dict = GetCustomTypes(UIResourceManager.WidgetFactory);
                        //Utils.Assert(dict.Contains(movie), $"Movie {movie} to be patched was not found in the WidgetFactory._customTypePaths!");
                        // remove widget from previously loaded Widgets
                        dict.Remove(movie);
                    }

                    // get internal dict of loaded Widgets
                    if (GetCustomTypePaths != null)
                    {
                        var dict = GetCustomTypePaths(UIResourceManager.WidgetFactory);
                        //Utils.Assert(dict.Contains(movie), $"Movie {movie} to be patched was not found in the WidgetFactory._customTypePaths!");
                        // remove widget from previously loaded Widgets
                        dict.Remove(movie);
                    }

                    // re-add it, forcing Factory to call now-patched `LoadFrom` method
                    UIResourceManager.WidgetFactory.AddCustomType(movie, moviePath);
                }
            }
        }

        /// <summary>
        /// Get path for movie from WidgetFactory
        /// </summary>
        /// <param name="movie"></param>
        private static string? PathForMovie(string movie)
        {
            // TODO: figure out a method more prone to game updates
            var prefabNamesMethod = AccessTools.DeclaredMethod(typeof(WidgetFactory), "GetPrefabNamesAndPathsFromCurrentPath");
            if (prefabNamesMethod == null || !(prefabNamesMethod.Invoke(UIResourceManager.WidgetFactory, Array.Empty<object>()) is Dictionary<string, string> paths))
            {
                Utils.DisplayUserError("UIExtenderEx could not find WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath!");
                return null;
            }

            return paths[movie];
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