using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Bannerlord.UIExtenderEx.Tests.Oracle;

/// <summary>
/// Compiles batches of generated prefab C# sources concurrently to amortize Roslyn compilation overhead, recursively bisecting failing batches to isolate individual compile failures.
/// </summary>
internal sealed class OracleCompiler
{
    private readonly RoslynCompiler _compiler = new();
    // Tracks process-wide assembly generation counts to prevent module naming collisions on runtime loaders.
    private static int _assemblies;

    public GameCompiledPrefabEnvironment Environment { get; } = new();

    public HashSet<Assembly> Assemblies { get; } = [];

    /// <summary>
    /// Compiles a collection of generated source units, returning successful assemblies or compiler diagnostic messages for failed individual units.
    /// </summary>
    public IEnumerable<(List<T> Group, Assembly? Assembly, IReadOnlyList<string> Errors)> Compile<T>(
        string namePrefix, List<T> group, Func<T, IEnumerable<GeneratedSource>> sourcesOf, IReadOnlyList<string> references, Func<byte[], string, Assembly>? load = null)
    {
        if (group.Count == 0)
            yield break;

        var name = $"{namePrefix}{System.Threading.Interlocked.Increment(ref _assemblies)}";
        var sources = group.SelectMany(sourcesOf).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = _compiler.Compile(name, sources, references);
        if (result.Success)
        {
            var assembly = load?.Invoke(result.Assembly!, name) ?? Environment.LoadAssembly(result.Assembly!);
            Assemblies.Add(assembly);
            yield return (group, assembly, []);
            yield break;
        }
        if (group.Count == 1)
        {
            yield return (group, null, result.Errors);
            yield break;
        }

        var half = group.Count / 2;
        foreach (var part in Compile(namePrefix, group.Take(half).ToList(), sourcesOf, references, load))
            yield return part;
        foreach (var part in Compile(namePrefix, group.Skip(half).ToList(), sourcesOf, references, load))
            yield return part;
    }

    /// <summary>Resolves the underlying public TaleWorlds widget base type represented by a generated proxy class.</summary>
    public Type VisibleType(Type type)
    {
        var current = type;
        while (current.BaseType is not null && Assemblies.Contains(current.Assembly))
            current = current.BaseType;
        return current;
    }

    private readonly Dictionary<Assembly, (System.Text.RegularExpressions.Regex? Names, Dictionary<string, string> Visible)> _generatedNames = [];

    /// <summary>
    /// Normalizes diagnostic text by replacing generated class identifiers with their underlying TaleWorlds widget type names.
    /// </summary>
    public string AsVisible(string text, Assembly assembly)
    {
        if (!_generatedNames.TryGetValue(assembly, out var names))
        {
            var visible = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var type in GetTypes(assembly).Where(x => typeof(TaleWorlds.GauntletUI.BaseTypes.Widget).IsAssignableFrom(x)))
                visible[type.Name] = VisibleType(type).Name;
            var pattern = string.Join("|", visible.Keys.OrderByDescending(x => x.Length).Select(System.Text.RegularExpressions.Regex.Escape));
            names = (visible.Count == 0 ? null : new System.Text.RegularExpressions.Regex($@"(?<![\w.])(?:{pattern})(?!\w)"), visible);
            _generatedNames[assembly] = names;
        }
        return names.Names is null ? text : names.Names.Replace(text, x => names.Visible[x.Value]);
    }

    private static IEnumerable<Type> GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(x => x is not null)!;
        }
    }
}