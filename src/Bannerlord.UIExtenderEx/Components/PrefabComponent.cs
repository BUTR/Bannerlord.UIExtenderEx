using Bannerlord.BUTR.Shared.Extensions;
using Bannerlord.BUTR.Shared.Helpers;
using Bannerlord.UIExtenderEx.Utils;

using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Components;

/// <summary>
/// Component that deals with Gauntlet prefab XML files
/// </summary>
internal partial class PrefabComponent
{
    internal sealed record PrefabPatch(Type Type, Action<XmlDocument> Patcher);

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
    internal readonly ConcurrentDictionary<string, List<PrefabPatch>> MoviePatches = new();
    private readonly ConcurrentDictionary<Type, bool> _enabledPatches = new();

    public PrefabComponent(string moduleName)
    {
        _moduleName = moduleName;
    }

    public IEnumerable<string> GetMoviesToPatch()
    {
        foreach (var (movie, patches) in MoviePatches)
        {
            if (patches.Any(x => _enabledPatches.TryGetValue(x.Type, out var enabled) && enabled))
                yield return movie;
        }
    }

    /// <summary>
    /// Enables all Prefabs.
    /// </summary>
    public void Enable()
    {
        foreach (var patchId in _enabledPatches.Keys)
            _enabledPatches[patchId] = true;
    }

    /// <summary>
    /// Disables all Prefabs.
    /// </summary>
    public void Disable()
    {
        foreach (var patchId in _enabledPatches.Keys)
            _enabledPatches[patchId] = false;
    }

    /// <summary>
    /// Enables a specific prefab.
    /// </summary>
    /// <param name="prefabType">The Prefab</param>
    public void Enable(Type prefabType)
    {
        if (_enabledPatches.ContainsKey(prefabType))
            _enabledPatches[prefabType] = true;
    }

    /// <summary>
    /// Disables a specific prefab.
    /// </summary>
    /// <param name="prefabType">The Prefab</param>
    public void Disable(Type prefabType)
    {
        if (_enabledPatches.ContainsKey(prefabType))
            _enabledPatches[prefabType] = false;
    }

    /// <summary>
    /// Register general XmlDocument patch
    /// </summary>
    /// <param name="movie"></param>
    /// <param name="prefabType"></param>
    /// <param name="patcher"></param>
    public void RegisterPatch(string movie, Type prefabType, Action<XmlDocument> patcher)
    {
        if (string.IsNullOrEmpty(movie))
        {
            MessageUtils.Fail("Invalid movie name!");
            return;
        }

        MoviePatches.GetOrAdd(movie, _ => new List<PrefabPatch>()).Add(new(prefabType, patcher));
        _enabledPatches[prefabType] = false;
    }

    /// <summary>
    /// Register general XmlDocument patch
    /// </summary>
    /// <param name="movie"></param>
    /// <param name="prefabType"></param>
    /// <param name="patcher"></param>
    public void RegisterPatch(string movie, Type prefabType, Action<XmlNode> patcher)
    {
        //RegisterPatch(movie, (XmlDocument node) => patcher(node));
        if (string.IsNullOrEmpty(movie))
        {
            MessageUtils.Fail("Invalid movie name!");
            return;
        }

        MoviePatches.GetOrAdd(movie, _ => new List<PrefabPatch>()).Add(new(prefabType, patcher));
        _enabledPatches[prefabType] = false;
    }

    /// <summary>
    /// Register patch operating at node specified by XPath
    /// </summary>
    /// <param name="movie"></param>
    /// <param name="xpath"></param>
    /// <param name="prefabType"></param>
    /// <param name="patcher"></param>
    public void RegisterPatch(string movie, string? xpath, Type prefabType, Action<XmlNode> patcher) => RegisterPatch(movie, prefabType, node =>
    {
        var node2 = node.SelectSingleNode(xpath ?? string.Empty);
        if (node2 is null)
        {
            MessageUtils.DisplayUserError($"Failed to apply extension to {movie}: node at {xpath} not found.");
            return;
        }

        patcher(node2);
    });

    public void Deregister()
    {
        MoviePatches.Clear();
        _enabledPatches.Clear();
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
        if (!MoviePatches.TryGetValue(movie, out var patches))
            return;

        if (_enabledPatches.Values.All(x => !x))
            return;

        foreach (var (id, patch) in patches)
        {
            if (!_enabledPatches.TryGetValue(id, out var enabled) || !enabled)
                continue;

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