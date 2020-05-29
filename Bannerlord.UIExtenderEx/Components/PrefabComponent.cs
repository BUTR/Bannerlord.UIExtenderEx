using Bannerlord.UIExtenderEx.Prefabs;
using Bannerlord.UIExtenderEx.ViewModels;

using HarmonyLib;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;

using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Components
{
    /// <summary>
    /// Component that deals with Gauntlet prefab XML files
    /// </summary>
    internal class PrefabComponent
    {
        private readonly Harmony _harmony;
        private readonly string _moduleName;

        /// <summary>
        /// Registered movie patches
        /// </summary>
        private readonly Dictionary<string, List<Action<XmlDocument>>> _moviePatches = new Dictionary<string, List<Action<XmlDocument>>>();
        
        public bool Enabled { get; private set; }
        
        public PrefabComponent(string moduleName)
        {
            _moduleName = moduleName;
            _harmony = new Harmony($"bannerlord.uiextender.ex.prefabs.{moduleName}");

            _harmony.Patch(
                AccessTools.Method(typeof(WidgetPrefab), nameof(WidgetPrefab.LoadFrom)),
                transpiler: new HarmonyMethod(new WrappedMethodInfo(AccessTools.Method(typeof(PrefabComponent), nameof(WidgetPrefab_LoadFrom_Transpiler)), this)));
        }

        /// <summary>
        /// Register general XmlDocument patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="patcher"></param>
        public void RegisterPatch(string movie, Action<XmlDocument> patcher)
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
        public void RegisterPatch(string movie, string? xpath, Action<XmlNode> patcher)
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
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertPatch patch)
        {
            RegisterPatch(movie, xpath, (node) =>
            {
                var extensionNode = patch.GetPrefabExtension().DocumentElement;
                var importedExtensionNode = node.OwnerDocument.ImportNode(extensionNode, true);
                var position = Math.Min(patch.Position, node.ChildNodes.Count - 1);
                position = Math.Max(position, 0);
                Debug.Assert(position >= 0 && position < node.ChildNodes.Count, $"Invalid position ({position}) for insert (patching in {patch.Id})");

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
            RegisterPatch(movie, xpath, (node) =>
            {
                var extensionNode = patch.GetPrefabExtension().DocumentElement;
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
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertAsSiblingPatch patch)
        {
            RegisterPatch(movie, xpath, (node) =>
            {
                var extensionNode = patch.GetPrefabExtension().DocumentElement;
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

        public void Enable()
        {
            Enabled = true;

            /*
            _harmony.Patch(
                AccessTools.Method(typeof(WidgetPrefab), nameof(WidgetPrefab.LoadFrom)),
                transpiler: new HarmonyMethod(new WrappedMethodInfo(AccessTools.Method(typeof(PrefabComponent), nameof(WidgetPrefab_LoadFrom_Transpiler)), this)));
            */
            ForceReloadMovies();
        }
        public void Disable()
        {
            Enabled = false;

            // Not working with Wrapped MethodInfo
            /*
            _harmony.Unpatch(
                AccessTools.Method(typeof(WidgetPrefab), nameof(WidgetPrefab.LoadFrom)),
                HarmonyPatchType.Transpiler);
            */

            ForceReloadMovies();
        }

        /// <summary>
        /// Make WidgetFactory reload Movies that were extended by _moviePatches.
        /// WidgetFactory loads Movies during SandBox module loading phase, which occurs even before
        /// our module gets loaded, hence once we get control we need to force it to reload XMLs that
        /// are getting patched by extensions.
        /// </summary>
        private void ForceReloadMovies()
        {
            // @TODO: figure out a method more prone to game updates
            
            // get internal dict of loaded Widgets
            var dict =  UIResourceManager.WidgetFactory.PrivateValue<IDictionary>("_customTypes");
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


        private IEnumerable<CodeInstruction> WidgetPrefab_LoadFrom_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var constructor = AccessTools.Constructor(typeof(WidgetPrefab));
            var loadMethod = AccessTools.Method(typeof(PrefabComponent), nameof(Load));

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Newobj && instruction.operand == constructor)
                {
                    var labels = instruction.labels;
                    instruction.labels = new List<Label>();
                    yield return new CodeInstruction(OpCodes.Ldstr, _moduleName) { labels = labels };
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Call, loadMethod);
                }

                yield return instruction;
            }
        }
        private static void Load(string moduleName, string path, XmlDocument document)
        {
            if (!(UIExtender.RuntimeFor(moduleName) is { } runtime) || !runtime.PrefabComponent.Enabled)
                return;

            var movieName = Path.GetFileNameWithoutExtension(path);
            runtime.PrefabComponent.ProcessMovieIfNeeded(movieName, document);
        }

        /// <summary>
        /// Apply patches to movie (if any is registered)
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="document"></param>
        private void ProcessMovieIfNeeded(string movie, XmlDocument document)
        {
            if (!_moviePatches.TryGetValue(movie, out var patches))
                return;

            foreach (var patch in patches)
            {
                patch(document);
            }
        }

        /// <summary>
        /// Get path for movie from WidgetFactory
        /// </summary>
        /// <param name="movie"></param>
        /// <returns></returns>
        private static string PathForMovie(string movie)
        {
            // @TODO: figure out a method more prone to game updates
            var prefabNamesMethod = typeof(WidgetFactory).GetMethod("GetPrefabNamesAndPathsFromCurrentPath", BindingFlags.Instance | BindingFlags.NonPublic);
            Utils.CompatAssert(prefabNamesMethod != null, "WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath");

            // get names and paths of loaded Widgets
            var paths = prefabNamesMethod.Invoke(UIResourceManager.WidgetFactory, Array.Empty<object>()) as Dictionary<string, string>;
            Utils.CompatAssert(paths != null, "WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath == null");

            return paths[movie];
        }
    }
}