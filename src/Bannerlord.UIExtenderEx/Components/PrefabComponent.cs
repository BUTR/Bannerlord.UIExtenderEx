using Bannerlord.BUTR.Shared.Helpers;
using Bannerlord.UIExtenderEx.Utils;

using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Components
{
    /// <summary>
    /// Component that deals with Gauntlet prefab XML files
    /// </summary>
    internal partial class PrefabComponent
    {
        private delegate Dictionary<string, string> GetPrefabNamesAndPathsFromCurrentPathDelegate(object instance);
        private static readonly GetPrefabNamesAndPathsFromCurrentPathDelegate? PrefabNamesMethod =
            AccessTools2.GetDeclaredDelegate<GetPrefabNamesAndPathsFromCurrentPathDelegate>(typeof(WidgetFactory), "GetPrefabNamesAndPathsFromCurrentPath");


        [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Keeping it for consistency>")]
        private readonly string _moduleName;

        /// <summary>
        /// Registered movie patches
        /// </summary>
        private readonly ConcurrentDictionary<string, List<Action<XmlDocument>>> _moviePatches = new();

        public bool Enabled { get; private set; }

        public PrefabComponent(string moduleName)
        {
            _moduleName = moduleName;
        }

        public void Enable()
        {
            Enabled = true;
        }
        public void Disable()
        {
            Enabled = false;
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
                MessageUtils.Fail("Invalid movie name!");
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
                MessageUtils.Fail("Invalid movie name!");
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
                MessageUtils.DisplayUserError($"Failed to apply extension to {movie}: node at {xpath} not found.");
                return;
            }

            patcher(node2);
        });


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
            if (PrefabNamesMethod?.Invoke(UIResourceManager.WidgetFactory) is { } paths)
            {
                return paths[movie];
            }
            else
            {
                MessageUtils.DisplayUserError("UIExtenderEx could not find WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath!");
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
            {
                return;
            }

            foreach (var patch in patches)
            {
                patch(document);
            }

            if (UIExtenderExSettings.Instance.DumpXML)
            {
                DumpXml(_moduleName, movie, document);
            }
        }

        private static void DumpXml(string moduleName, string movie, XmlDocument document)
        {
            if (ModuleInfoHelper.GetModuleByType(typeof(SubModule)) is { } module)
            {
                var dumpPath = Path.Combine(module.Path, "Dumps", $"{movie}_{moduleName}.xml");
                var file = new FileInfo(dumpPath);
                file.Directory?.Create();
                using var fs = file.Open(FileMode.OpenOrCreate, FileAccess.Write);
                fs.SetLength(0);
                using var writer = new StreamWriter(fs);
                using var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    NewLineChars = Environment.NewLine,
                    NewLineHandling = NewLineHandling.Replace
                });
                document.Save(xmlWriter);
            }
        }
    }
}