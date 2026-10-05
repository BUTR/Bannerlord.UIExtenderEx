using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Analyzers.Game;

/// <summary>Represents the data-binding scope at a node in a game prefab, identified by type name from <c>types.json</c>.</summary>
internal abstract record GameScope;

internal sealed record ViewModelGameScope(string Type) : GameScope;

internal sealed record ListGameScope(string ElementType) : GameScope;

internal sealed record UnknownGameScope : GameScope
{
    public static readonly UnknownGameScope Instance = new();
}

/// <summary>Represents the evaluation context of a prefab usage, including outer scope chain and passed parameters.</summary>
internal sealed record PrefabContext(ImmutableList<GameScope> Chain, IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// Resolves the data-binding scope at any element within a game prefab DOM, mirroring the loader's binding traversal:
/// movie roots bind to paired ViewModels, <c>DataSource</c> attributes traverse property paths,
/// <c>ItemTemplate</c> elements bind list item types, and tag-instantiated child prefabs inherit context and parameters.
/// </summary>
internal sealed class GameScopeResolver
{
    private const int MaxDepth = 6;
    private const int MaxContexts = 32;

    private readonly GameConfiguration _configuration;
    private readonly CancellationToken _cancellation;
    private readonly Dictionary<string, IReadOnlyList<PrefabContext>> _contexts = new(StringComparer.Ordinal);

    public GameScopeResolver(GameConfiguration configuration, CancellationToken cancellation)
    {
        _configuration = configuration;
        _cancellation = cancellation;
    }

    /// <summary>
    /// Resolves active data-binding scopes at <paramref name="element"/> within <paramref name="prefab"/>.
    /// If <paramref name="inside"/> is <see langword="true"/>, evaluates scopes within the element (following its own <c>DataSource</c>);
    /// otherwise evaluates scopes surrounding the element.
    /// </summary>
    public IReadOnlyList<GameScope> ScopesAt(GamePrefab prefab, XmlElement element, bool inside)
    {
        var result = new List<GameScope>();
        foreach (var context in ContextsOf(prefab.Name, 0, []))
        {
            var chain = Walk(element, context, inside);
            var scope = chain.Count > 0 ? chain[chain.Count - 1] : UnknownGameScope.Instance;
            if (!result.Contains(scope))
                result.Add(scope);
        }
        return result;
    }

    private IReadOnlyList<PrefabContext> ContextsOf(string name, int depth, HashSet<string> visiting)
    {
        if (_contexts.TryGetValue(name, out var cached))
            return cached;
        if (depth > MaxDepth || !visiting.Add(name) || _configuration.Prefab(name) is not { } prefab || prefab.Document(_cancellation) is not { } document)
            return [];

        var result = new List<PrefabContext>();
        var defaults = Defaults(document);
        foreach (var movie in _configuration.MoviesNamed(name))
        {
            GameScope root = movie.ViewModel is { } vm ? new ViewModelGameScope(vm) : UnknownGameScope.Instance;
            Add(result, new PrefabContext(ImmutableList.Create(root), defaults));
        }

        // Evaluates tag usages in referencing prefabs, using the prefab index to filter candidates.
        foreach (var user in _configuration.Prefabs)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (result.Count >= MaxContexts)
                break;
            if (user.Name == name || !user.Tags.Contains(name) || user.Document(_cancellation) is not { } userDocument)
                continue;
            foreach (XmlElement tag in userDocument.GetElementsByTagName(name))
            {
                foreach (var userContext in ContextsOf(user.Name, depth + 1, visiting))
                {
                    var chain = Walk(tag, userContext, inside: true);
                    var parameters = defaults.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
                    foreach (XmlAttribute attribute in tag.Attributes)
                    {
                        if (attribute.Name.StartsWith("Parameter.", StringComparison.Ordinal))
                            parameters[attribute.Name.Substring("Parameter.".Length)] = Substitute(attribute.Value, userContext.Parameters) ?? "";
                    }
                    Add(result, new PrefabContext(chain, parameters));
                }
            }
        }

        visiting.Remove(name);
        _contexts[name] = result;
        return result;
    }

    private static void Add(List<PrefabContext> contexts, PrefabContext context)
    {
        if (contexts.Count < MaxContexts && !contexts.Any(x => x.Chain.SequenceEqual(context.Chain) && SameParameters(x.Parameters, context.Parameters)))
            contexts.Add(context);
    }

    private static bool SameParameters(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(x => b.TryGetValue(x.Key, out var value) && value == x.Value);

    private static IReadOnlyDictionary<string, string> Defaults(XmlDocument document)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        // Parses child elements under <Parameters>, matching WidgetPrefab.LoadParameters behavior.
        if (document.DocumentElement is { Name: "Prefab" } prefab && prefab["Parameters"] is { } parameters)
        {
            foreach (var parameter in parameters.ChildNodes.OfType<XmlElement>())
            {
                if (parameter.GetAttribute("Name") is { Length: > 0 } name)
                    result[name] = parameter.GetAttribute("DefaultValue");
            }
        }
        return result;
    }

    /// <summary>Walks the hierarchy downward from the root widget to compute the scope chain at <paramref name="target"/>.</summary>
    private ImmutableList<GameScope> Walk(XmlElement target, PrefabContext context, bool inside)
    {
        // From the target up to the root widget, the element under Window
        var path = new List<XmlElement>();
        for (XmlNode? node = target; node is XmlElement element && element.Name != "Window" && element.Name != "Prefab"; node = node.ParentNode)
            path.Add(element);
        path.Reverse();

        var chain = context.Chain;
        foreach (var element in path)
        {
            switch (element.Name)
            {
                case "Children":
                case "ItemTemplates":
                    continue;
                case "ItemTemplate":
                    chain = chain.Add(chain[chain.Count - 1] is ListGameScope list ? ChildScope(list.ElementType) : UnknownGameScope.Instance);
                    continue;
            }
            if (ReferenceEquals(element, target) && !inside)
                break;
            if (element.GetAttributeNode("DataSource") is { } dataSource)
            {
                var value = Substitute(dataSource.Value, context.Parameters);
                chain = value is { Length: > 2 } && value[0] == '{' && value[value.Length - 1] == '}'
                    ? Resolve(chain, value.Substring(1, value.Length - 2))
                    : chain.Add(UnknownGameScope.Instance);
            }
        }
        return chain;
    }

    private ImmutableList<GameScope> Resolve(ImmutableList<GameScope> chain, string path)
    {
        foreach (var node in path.Split('\\'))
        {
            if (node.Length == 0)
                continue;
            if (node == "..")
            {
                chain = chain.Count > 1 ? chain.RemoveAt(chain.Count - 1) : ImmutableList.Create<GameScope>(UnknownGameScope.Instance);
                continue;
            }
            chain = chain.Add(chain[chain.Count - 1] switch
            {
                ViewModelGameScope vm => PropertyType(vm.Type, node) is { } type ? ChildScope(type) : UnknownGameScope.Instance,
                ListGameScope list when int.TryParse(node, out _) => ChildScope(list.ElementType),
                _ => UnknownGameScope.Instance,
            });
        }
        return chain;
    }

    private string? PropertyType(string viewModel, string name)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var type = _configuration.ViewModel(viewModel); type is not null && seen.Add(type.Type); type = type.BaseType is { } b ? _configuration.ViewModel(b) : null)
        {
            if (type.Properties.TryGetValue(name, out var propertyType))
                return propertyType;
        }
        return null;
    }

    private GameScope ChildScope(string type)
    {
        const string list = "TaleWorlds.Library.MBBindingList<";
        if (type.StartsWith(list, StringComparison.Ordinal) && type.EndsWith(">", StringComparison.Ordinal))
            return new ListGameScope(type.Substring(list.Length, type.Length - list.Length - 1));
        return _configuration.ViewModel(type) is not null ? new ViewModelGameScope(type) : UnknownGameScope.Instance;
    }

    /// <summary>Substitutes prefab parameter references of the form <c>*Name</c> with passed parameter values.</summary>
    private static string? Substitute(string value, IReadOnlyDictionary<string, string> parameters) =>
        !value.StartsWith("*", StringComparison.Ordinal) ? value : parameters.TryGetValue(value.Substring(1), out var passed) ? passed : null;
}