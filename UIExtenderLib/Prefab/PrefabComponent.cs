using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using UIExtenderLib.Interface;
using NonGenericCollections = System.Collections;
using Path = System.IO.Path;

namespace UIExtenderLib.Prefab
{
    /// <summary>
    /// Component that deals with Gauntlet prefab XML files
    /// </summary>
    public class PrefabComponent
    {
        private readonly string _moduleName;
        
        /// <summary>
        /// Registered movie patches
        /// </summary>
        private Dictionary<string, List<Action<XmlDocument>>> _moviePatches = new Dictionary<string, List<Action<XmlDocument>>>();

        public PrefabComponent(string moduleName)
        {
            _moduleName = moduleName;
        }

        /// <summary>
        /// Register general XmlDocument patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="patcher"></param>
        internal void RegisterPatch(string movie, Action<XmlDocument> patcher)
        {
            Debug.Assert(movie != null && !movie.IsEmpty(), $"Invalid movie name: {movie}!");

            _moviePatches.Get(movie, () => new List<Action<XmlDocument>>()).Add(patcher);
        }

        /// <summary>
        /// Register patch operating at node specified by XPath
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patcher"></param>
        internal void RegisterPatch(string movie, string xpath, Action<XmlNode> patcher)
        {
            RegisterPatch(movie, (document) =>
            {
                var node = document.SelectSingleNode(xpath);
                if (node == null)
                {
                    Utils.DisplayUserError($"Failed to apply extension to {movie}: node at {xpath} not found.");
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
        internal void RegisterPatch(string movie, string xpath, PrefabExtensionInsertPatch patch)
        {
            RegisterPatch(movie, xpath, (node) =>
            {
                var extensionNode = LoadPrefabExtension(patch.Name);
                var importedExtensionNode = node.OwnerDocument.ImportNode(extensionNode, true);
                var position = Math.Min(patch.Position, node.ChildNodes.Count - 1);
                position = Math.Max(position, 0);
                Debug.Assert(position >= 0 && position < node.ChildNodes.Count, $"Invalid position ({position}) for insert (patching in {patch.Name})");

                node.InsertAfter(importedExtensionNode, node.ChildNodes[position]);
            });
        }

        /// <summary>
        /// Register snippet replace patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        internal void RegisterPatch(string movie, string xpath, PrefabExtensionReplacePatch patch)
        {
            RegisterPatch(movie, xpath, (node) =>
            {
                var extensionNode = LoadPrefabExtension(patch.Name);
                var importedExtensionNode = node.OwnerDocument.ImportNode(extensionNode, true);

                node.ParentNode.ReplaceChild(importedExtensionNode, node);
            });
        }

        /// <summary>
        /// Register snippet insert as sibling patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        internal void RegisterPatch(string movie, string xpath, PrefabExtensionInsertAsSiblingPatch patch)
        {
            RegisterPatch(movie, xpath, (node) =>
            {
                var extensionNode = LoadPrefabExtension(patch.Name);
                var importedExtensionNode = node.OwnerDocument.ImportNode(extensionNode, true);

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

        /**
         * Load and parse prefab extension
         */
        /// <summary>
        /// Load snippet extension XML. Will search for the file in MODULE\GUI\PrefabExtensions\NAME.xml
        /// </summary>
        /// <param name="name">name of the extension (without .xml)</param>
        /// <returns>document element</returns>
        private XmlNode LoadPrefabExtension(string name)
        {
            var path = Path.Combine(Utilities.GetBasePath(), "Modules", _moduleName, "GUI", "PrefabExtensions", name + ".xml");
            var doc = new XmlDocument();
                
            using (var reader = XmlReader.Create(path, new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
            }))
            {
                doc.Load(reader);
            }
                
            Debug.Assert(doc.HasChildNodes, $"Failed to parse extension ({name}) XML!");
            return doc.DocumentElement;
        }

        /// <summary>
        /// Make WidgetFactory reload Movies that were extended by _moviePatches.
        /// WidgetFactory loads Movies during SandBox module loading phase, which occurs even before
        /// our module gets loaded, hence once we get control we need to force it to reload XMLs that
        /// are getting patched by extensions.
        /// </summary>
        internal void ForceReloadMovies()
        {
            // @TODO: figure out a method more prone to game updates
            
            // get internal dict of loaded Widgets
            var dict =  UIResourceManager.WidgetFactory.PrivateValue<NonGenericCollections.IDictionary>("_customTypes");
            Utils.CompatAssert(dict != null, "WidgetFactory._customTypes == null");
            
            foreach (var movie in _moviePatches.Keys)
            {
                Debug.Assert(dict.Contains(movie), $"Movie {movie} to be patched was not found in the WidgetFactory!");
                
                // remove widget from previously loaded Widgets
                dict.Remove(movie);
                
                // re-add it, forcing Factory to call now-patched `LoadFrom` method
                UIResourceManager.WidgetFactory.AddCustomType(movie, PathForMovie(movie));
            }
        }

        /// <summary>
        /// Get path for movie from WidgetFactory
        /// </summary>
        /// <param name="movie"></param>
        /// <returns></returns>
        private string PathForMovie(string movie)
        {
            // @TODO: figure out a method more prone to game updates
            var prefabNamesMethod = typeof(WidgetFactory).GetMethod("GetPrefabNamesAndPathsFromCurrentPath", BindingFlags.Instance | BindingFlags.NonPublic);
            Utils.CompatAssert(prefabNamesMethod != null, "WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath");
            
            // get names and paths of loaded Widgets
            var paths = prefabNamesMethod.Invoke(UIResourceManager.WidgetFactory, new object[] { }) as Dictionary<string, string>;
            Utils.CompatAssert(paths != null, "WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath == null");
            
            return paths[movie];
        }

        /// <summary>
        /// Apply patches to movie (if any is registered)
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="document"></param>
        public void ProcessMovieIfNeeded(string movie, XmlDocument document)
        {
            if (_moviePatches.ContainsKey(movie))
            {
                foreach (var patch in _moviePatches[movie])
                {
                    patch(document);
                }
            }
        }
    }
}