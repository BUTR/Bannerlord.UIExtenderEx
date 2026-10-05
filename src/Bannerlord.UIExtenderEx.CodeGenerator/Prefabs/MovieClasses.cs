using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;

using System.Collections.Generic;

using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library.CodeGeneration;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

/// <summary>
/// Manages the collection of secondary classes generated for a movie, including rooted prefabs, nested prefab instantiations, and item templates.
/// </summary>
/// <remarks>
/// Generates sequentially numbered dependency class names and emits them following the primary movie class definition.
/// </remarks>
internal sealed class MovieClasses
{
    private readonly List<PrefabClass> _classes = [];

    private readonly string _movieClassName;

    private int _count;

    public MovieClasses(string movieName, string movieClassName)
    {
        MovieName = movieName;
        _movieClassName = movieClassName;
    }

    /// <summary>Gets the name of the root movie associated with these generated classes.</summary>
    public string MovieName { get; }

    /// <summary>Generates a unique dependency class name for the next required prefab class.</summary>
    public string NextName() => $"{_movieClassName}_Dependency_{++_count}";

    public void Add(PrefabClass prefabClass) => _classes.Add(prefabClass);

    /// <summary>
    /// Searches for an existing generated <see cref="PrefabClass"/> matching the specified kind, prefab name, options, and parameter templates.
    /// </summary>
    /// <returns>The matching <see cref="PrefabClass"/> instance, or <see langword="null"/> if not found.</returns>
    public PrefabClass? Find(PrefabClassKind kind, string prefabName, PrefabClassOptions options,
        Dictionary<string, WidgetAttributeTemplate> givenParameters, Dictionary<string, WidgetAttributeTemplate> bindingParameters)
    {
        foreach (var prefabClass in _classes)
        {
            if (prefabClass.Kind == kind && prefabClass.PrefabName == prefabName && prefabClass.Options == options
                && SameParameters(givenParameters, prefabClass.Values.GivenParameters)
                && SameParameters(bindingParameters, prefabClass.Values.BindingParameters))
            {
                return prefabClass;
            }
        }
        return null;
    }

    private static bool SameParameters(Dictionary<string, WidgetAttributeTemplate> requested, Dictionary<string, WidgetAttributeTemplate> existing)
    {
        if (requested.Count != existing.Count)
        {
            return false;
        }
        foreach (var (parameterName, requestedParameter) in requested)
        {
            if (!existing.TryGetValue(parameterName, out var existingParameter)
                || requestedParameter.Value != existingParameter.Value
                || requestedParameter.KeyType.GetType() != existingParameter.KeyType.GetType()
                || requestedParameter.ValueType.GetType() != existingParameter.ValueType.GetType())
            {
                return false;
            }
        }
        return true;
    }

    public IReadOnlyList<PrefabClass> All => _classes;

    /// <summary>Prepares all queued classes recursively (see <see cref="PrefabClass.Prepare"/>).</summary>
    public void Prepare()
    {
        // Uses indexed iteration because class preparation can dynamically append nested dependency classes to the list.
        for (var i = 0; i < _classes.Count; i++)
        {
            _classes[i].Prepare();
        }
    }

    public void GenerateInto(NamespaceCode namespaceCode)
    {
        // Emits all prepared classes sequentially into the target namespace.
        foreach (var prefabClass in _classes)
        {
            prefabClass.GenerateInto(namespaceCode);
        }
    }
}