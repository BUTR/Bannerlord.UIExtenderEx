using Bannerlord.BUTR.Shared.Extensions;
using Bannerlord.BUTR.Shared.Helpers;
using Bannerlord.UIExtenderEx.ResourceManager;
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

namespace Bannerlord.UIExtenderEx.Components;

/// <summary>
/// Manages registration, filtering, and execution of Gauntlet prefab XML patches.
/// </summary>
internal partial class PrefabComponent
{
    internal sealed record PrefabPatch(Type Type, Action<XmlDocument> Patcher);

    private delegate Dictionary<string, string> GetPrefabNamesAndPathsFromCurrentPathDelegate(object instance);
    private static readonly GetPrefabNamesAndPathsFromCurrentPathDelegate? PrefabNamesMethod =
        AccessTools2.GetDeclaredDelegate<GetPrefabNamesAndPathsFromCurrentPathDelegate>("TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:GetPrefabNamesAndPathsFromCurrentPath");


    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For ReSharper")]
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    [SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Keeping it for consistency>")]
    private readonly string _moduleName;

    /// <summary>
    /// Registered movie patch collections keyed by movie name.
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
            if (patches.Any(IsEnabled))
                yield return movie;
        }
    }

    /// <summary>Determines whether <see cref="ProcessMovieIfNeeded"/> applies modifications to the specified movie.</summary>
    public bool HasEnabledPatches(string movie) => MoviePatches.TryGetValue(movie, out var patches) && patches.Any(IsEnabled);

    private bool IsEnabled(PrefabPatch patch) => _enabledPatches.TryGetValue(patch.Type, out var enabled) && enabled;

    /// <summary>
    /// Enables all registered prefab patches managed by this component.
    /// </summary>
    public void Enable()
    {
        foreach (var patchId in _enabledPatches.Keys)
            _enabledPatches[patchId] = true;
        WidgetFactoryManager.ReloadOnNextUse(MoviePatches.Keys);
    }

    /// <summary>
    /// Disables all registered prefab patches managed by this component.
    /// </summary>
    public void Disable()
    {
        foreach (var patchId in _enabledPatches.Keys)
            _enabledPatches[patchId] = false;
        WidgetFactoryManager.ReloadOnNextUse(MoviePatches.Keys);
    }

    /// <summary>
    /// Enables all patches defined by the specified prefab patch type.
    /// </summary>
    /// <param name="prefabType">The prefab patch type to enable.</param>
    public void Enable(Type prefabType)
    {
        if (_enabledPatches.ContainsKey(prefabType))
            _enabledPatches[prefabType] = true;
        WidgetFactoryManager.ReloadOnNextUse(GetMoviesPatchedBy(prefabType));
    }

    /// <summary>
    /// Disables all patches defined by the specified prefab patch type.
    /// </summary>
    /// <param name="prefabType">The prefab patch type to disable.</param>
    public void Disable(Type prefabType)
    {
        if (_enabledPatches.ContainsKey(prefabType))
            _enabledPatches[prefabType] = false;
        WidgetFactoryManager.ReloadOnNextUse(GetMoviesPatchedBy(prefabType));
    }

    private IEnumerable<string> GetMoviesPatchedBy(Type prefabType) =>
        MoviePatches.Where(x => x.Value.Any(patch => patch.Type == prefabType)).Select(x => x.Key);

    /// <summary>
    /// Registers an XML document patch operating on the root <see cref="XmlDocument"/>.
    /// </summary>
    /// <param name="movie">The target movie name.</param>
    /// <param name="prefabType">The identifying prefab patch type.</param>
    /// <param name="patcher">The delegate applying modifications to the document.</param>
    public void RegisterPatch(string movie, Type prefabType, Action<XmlDocument> patcher)
    {
        if (string.IsNullOrEmpty(movie))
        {
            MessageUtils.Fail("Invalid movie name!");
            return;
        }

        MoviePatches.GetOrAdd(movie, _ => []).Add(new(prefabType, patcher));
        _enabledPatches[prefabType] = false;
    }

    /// <summary>
    /// Registers an XML patch operating on the root <see cref="XmlNode"/>.
    /// </summary>
    /// <param name="movie">The target movie name.</param>
    /// <param name="prefabType">The identifying prefab patch type.</param>
    /// <param name="patcher">The delegate applying modifications to the root node.</param>
    public void RegisterPatch(string movie, Type prefabType, Action<XmlNode> patcher)
    {
        //RegisterPatch(movie, (XmlDocument node) => patcher(node));
        if (string.IsNullOrEmpty(movie))
        {
            MessageUtils.Fail("Invalid movie name!");
            return;
        }

        MoviePatches.GetOrAdd(movie, _ => []).Add(new(prefabType, patcher));
        _enabledPatches[prefabType] = false;
    }

    /// <summary>
    /// Registers an XML patch targeting an <see cref="XmlNode"/> selected via XPath.
    /// </summary>
    /// <param name="movie">The target movie name.</param>
    /// <param name="xpath">The XPath expression selecting the target node.</param>
    /// <param name="prefabType">The identifying prefab patch type.</param>
    /// <param name="patcher">The delegate applying modifications to the target node.</param>
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
        // Invalidate cached prefabs before clearing state so active factories reload clean XML definitions.
        var patched = MoviePatches.Keys.ToList();
        MoviePatches.Clear();
        _enabledPatches.Clear();
        WidgetFactoryManager.ReloadOnNextUse(patched);
    }


    /// <summary>
    /// Removes XML comment nodes to prevent engine crashes when Gauntlet processes modified templates.
    /// Returns <see langword="false"/> if <paramref name="node"/> is a comment node or <see langword="null"/>.
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
            xmlNode.ParentNode?.RemoveChild(xmlNode);
        }

        return true;
    }

    /// <summary>
    /// Retrieves the file path corresponding to the specified movie name from <see cref="WidgetFactory"/>.
    /// </summary>
    private static string? PathForMovie(string movie)
    {
        if (PrefabNamesMethod?.Invoke(UIResourceManager.WidgetFactory) is not { } paths)
        {
            MessageUtils.DisplayUserError("UIExtenderEx could not find WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath!");
            return null;
        }

        return paths[movie];
    }

    /// <summary>
    /// Applies all active patches registered for the specified movie to the provided <see cref="XmlDocument"/>.
    /// </summary>
    /// <param name="movie">The name of the movie being processed.</param>
    /// <param name="document">The XML document representing the movie template.</param>
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
            using var xmlWriter = XmlWriter.Create(writer, new()
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