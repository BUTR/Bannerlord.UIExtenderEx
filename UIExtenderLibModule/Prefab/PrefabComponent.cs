using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NonGenericCollections = System.Collections;
using System.Reflection;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.MountAndBlade.Source.Objects.Siege;
using UIExtenderLib.Prefab;
using Path = System.IO.Path;

namespace UIExtenderLibModule.Prefab
{
    internal class PrefabComponent
    {
        private Dictionary<string, List<Action<XmlDocument>>> _moviePatches = new Dictionary<string, List<Action<XmlDocument>>>();
        private Dictionary<string, string> _prefabExtensionPaths = new Dictionary<string, string>();

        /**
         * Register general patcher.
         * Patcher is an lambda function returning patched XmlDocument.
         */
        internal void RegisterPatch(string movie, Action<XmlDocument> patcher)
        {
            Debug.Assert(movie != null && !movie.IsEmpty(), $"Invalid movie name: {movie}!");

            _moviePatches.Get(movie, () => new List<Action<XmlDocument>>()).Add(patcher);
        }

        /**
         * Register custom patch for node at xpath
         */
        internal void RegisterPatch(string movie, string xpath, Action<XmlNode> patcher)
        {
            RegisterPatch(movie, (document) =>
            {
                var node = document.SelectSingleNode(xpath);
                if (node == null)
                {
                    Utils.UserError($"Failed to apply extension to {movie}: node at {xpath} not found.");
                    return;
                }

                patcher(node);
            });
        }

        /**
         * Register XML insert patch
         */
        internal void RegisterPatch(string movie, string xpath, PrefabExtensionInsertPatch patch)
        {
            Debug.Assert(_prefabExtensionPaths.ContainsKey(patch.Name), $"Prefab extension with name {patch.Name} does not exist!");

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

        /**
         * Register XML replace patch
         */
        internal void RegisterPatch(string movie, string xpath, PrefabExtensionReplacePatch patch)
        {
            RegisterPatch(movie, xpath, (node) =>
            {
                var extensionNode = LoadPrefabExtension(patch.Name);
                var importedExtensionNode = node.OwnerDocument.ImportNode(extensionNode, true);

                node.ParentNode.ReplaceChild(importedExtensionNode, node);
            });
        }

        /**
         * Register XML sibling insert patch
         */
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
         * Search loaded modules for PrefabExtensions and save them for further reference
         */
        internal void FindPrefabExtensions()
        {
            foreach (var module in Utilities.GetModulesNames())
            {
                var info = new DirectoryInfo(Path.Combine(Utilities.GetBasePath(), "Modules", module, "GUI", "PrefabExtensions"));
                if (!info.Exists)
                {
                    continue;
                }

                foreach (var file in info.GetFiles("*.xml", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(file.FullName);
                    _prefabExtensionPaths[name] = file.FullName;
                }
            }
        }

        /**
         * Load and parse prefab extension
         */
        private XmlNode LoadPrefabExtension(string name)
        {
            var path = _prefabExtensionPaths[name];
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

        /**
         * Make WidgetFactory reload Movies that were extended by _moviePatches.
         * 
         * WidgetFactory loads Movies during SandBox module loading phase, which occurs even before
         * our module gets loaded, hence once we get control we need to force it to reload XMLs that
         * are getting patched by extensions.
         */
        internal void ForceReloadMovies()
        {
            // @TODO: figure out a method more prone to game updates
            
            // get internal dict of loaded Widgets
            var dict =  UIResourceManager.WidgetFactory.PrivateValue<NonGenericCollections.IDictionary>("_customTypes");
            Utils.CompatiblityAssert(dict != null, "WidgetFactory._customTypes == null");
            
            foreach (var movie in _moviePatches.Keys)
            {
                Debug.Assert(dict.Contains(movie), $"Movie {movie} to be patched was not found in the WidgetFactory!");
                
                // remove widget from previously loaded Widgets
                dict.Remove(movie);
                
                // re-add it, forcing Factory to call now-patched `LoadFrom` method
                UIResourceManager.WidgetFactory.AddCustomType(movie, PathForMovie(movie));
            }
        }

        /**
         * Get path for movie from WidgetFactory
         */
        private string PathForMovie(string movie)
        {
            // @TODO: figure out a method more prone to game updates
            var prefabNamesMethod = typeof(WidgetFactory).GetMethod("GetPrefabNamesAndPathsFromCurrentPath", BindingFlags.Instance | BindingFlags.NonPublic);
            Utils.CompatiblityAssert(prefabNamesMethod != null, "WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath");
            
            // get names and paths of loaded Widgets
            var paths = prefabNamesMethod.Invoke(UIResourceManager.WidgetFactory, new object[] { }) as Dictionary<string, string>;
            Utils.CompatiblityAssert(paths != null, "WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath == null");
            
            return paths[movie];
        }

        /**
         * Apply patches to movie
         */
        private void ProcessMovieIfNeeded(string movie, XmlDocument document)
        {
            if (_moviePatches.ContainsKey(movie))
            {
                foreach (var patch in _moviePatches[movie])
                {
                    patch(document);
                }
            }
        }

        /**
         * Method that is called by transpiled `WidgetPrefab.LoadFrom`
         *
         * Called from generated IL in `WidgetPrefabLoadPatch` transpiler
         */
        public static void ProcessMovieDocumentIfNeeded(string path, XmlDocument document)
        {
            // equivalent XML parsing code from `WidgetPrefab.LoadFrom`
            // needs to be duplicated here since original gets patched out to free up space in IL
            using (XmlReader xmlReader = XmlReader.Create(path, new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
            }))
            {
                document.Load(xmlReader);
            }

            // actually apply patches to document
            UIExtenderLibModule.SharedInstance.WidgetComponent.ProcessMovieIfNeeded(Path.GetFileNameWithoutExtension(path), document);
        }
    }
}