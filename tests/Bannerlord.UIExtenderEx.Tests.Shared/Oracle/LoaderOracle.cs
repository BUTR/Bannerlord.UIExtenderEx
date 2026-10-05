using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

using NSubstitute;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.Tests.Oracle;

public enum OracleOutcome
{
    /// <summary>Indicates that both widget trees instantiated successfully with identical observable state.</summary>
    Match,
    /// <summary>Indicates that both widget trees instantiated but exhibit observable property discrepancies.</summary>
    Differs,
    /// <summary>Indicates that the interpreted XML loader failed on this prefab, leaving no baseline behavior to match.</summary>
    XmlThrows,
    /// <summary>Indicates that the XML loader instantiated successfully while the compiled prefab threw an exception.</summary>
    CompiledThrows,
    /// <summary>
    /// Indicates that the code generator declined a prefab that the interpreted XML loader successfully instantiates.
    /// </summary>
    NotGenerated,
    /// <summary>Indicates that Roslyn failed to compile the generated C# source code.</summary>
    NotCompiled,
    /// <summary>Indicates that the test harness encountered an infrastructure failure during setup.</summary>
    HarnessFailed,
    /// <summary>Indicates that the generator intentionally declined the prefab according to <see cref="OracleDeclines.Intended"/>.</summary>
    Declined,
}

/// <summary>Classifies code generation declines against interpreted XML loader behavior.</summary>
public static class OracleDeclines
{
    /// <summary>
    /// Maps substrings of recognized diagnostic messages to documented reasons for intentional code generation declines.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Intended = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Maps error substrings identifying fatal XML constructs, such as circular constant references that trigger stack overflows.</summary>
    private static readonly IReadOnlyDictionary<string, string> FatalToXml = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["which refers to itself"] = "the loader resolves a constant that refers to itself until the stack overflows",
    };

    /// <summary>
    /// Classifies an exception raised during code generation against interpreted XML loader behavior.
    /// </summary>
    /// <param name="prefab">The name of the prefab being evaluated.</param>
    /// <param name="decline">The exception thrown during code generation.</param>
    /// <param name="buildXml">A callback that instantiates the prefab via XML to assess baseline behavior.</param>
    public static OracleResult Classify(string prefab, Exception decline, Func<string?> buildXml)
    {
        var reason = LoaderOracle.Describe(decline);
        // Bypasses XML test instantiation for constructs known to trigger fatal process crashes.
        if (FatalToXml.FirstOrDefault(x => reason.Contains(x.Key)) is { Key: not null } fatal)
            return new(prefab, OracleOutcome.XmlThrows, [], $"not built from XML: {fatal.Value}{Environment.NewLine}declined: {reason}", 0, 0);
        var xmlAsserts = new List<string>();
        string? xmlFailure;
        using (LoaderOracle.RecordAsserts(xmlAsserts))
        {
            try
            {
                xmlFailure = buildXml();
            }
            catch (Exception e)
            {
                xmlFailure = LoaderOracle.Describe(e);
            }
        }
        if (xmlFailure is not null)
            return new(prefab, OracleOutcome.XmlThrows, [], $"{xmlFailure}{Environment.NewLine}declined as well: {reason}", xmlAsserts.Count, 0);
        var intended = Intended.Keys.FirstOrDefault(x => reason.Contains(x));
        return intended is null
            ? new(prefab, OracleOutcome.NotGenerated, [], reason, xmlAsserts.Count, 0)
            : new(prefab, OracleOutcome.Declined, [], $"{reason}{Environment.NewLine}intended: {Intended[intended]}", xmlAsserts.Count, 0);
    }
}

public sealed record OracleResult(string Prefab, OracleOutcome Outcome, IReadOnlyList<string> Differences, string? Detail, int XmlAsserts, int CompiledAsserts)
{
    /// <summary>
    /// Indicates whether the verification result represents a functional discrepancy, unexpected exception, or assertion mismatch.
    /// </summary>
    public bool IsDefect => Outcome is OracleOutcome.Differs or OracleOutcome.CompiledThrows or OracleOutcome.NotCompiled or OracleOutcome.NotGenerated
                            || (Outcome is OracleOutcome.Match && XmlAsserts != CompiledAsserts);
}

/// <summary>
/// Compares compiled prefab instantiation against the interpreted XML loader baseline.
/// <para>
/// Instantiates each prefab through both runtime pipelines: interpreted instantiation via <see cref="WidgetPrefab.Instantiate"/>
/// and compiled instantiation via generated C# widget classes. Traverses and compares both resulting widget hierarchies via
/// <see cref="WidgetSnapshot"/> while recording diagnostic assertions to verify structural and attribute parity.
/// </para>
/// <para>
/// Evaluates static prefab instantiation without active data sources; bindings remain literal text and item templates remain unexpanded.
/// </para>
/// </summary>
public sealed class LoaderOracle
{
    private const string Namespace = "Bannerlord.UIExtenderEx.AutoGenerated.Oracle";

    private static int _batch;

    private readonly WidgetFactory _widgetFactory;
    private readonly SpriteData _spriteData;
    private readonly BrushFactory _brushFactory;
    private readonly UIContext _context;
    private readonly IReadOnlyList<string> _references;
    private readonly OracleCompiler _compiler = new();

    public LoaderOracle(WidgetFactory widgetFactory, SpriteData spriteData, BrushFactory brushFactory, UIContext context, IEnumerable<Assembly> widgetAssemblies)
    {
        _widgetFactory = widgetFactory;
        _spriteData = spriteData;
        _brushFactory = brushFactory;
        _context = context;
        var seeds = new HashSet<Assembly>(widgetAssemblies)
        {
            typeof(ViewModel).Assembly, typeof(Widget).Assembly, typeof(WidgetPrefab).Assembly, typeof(SpriteData).Assembly, typeof(PrefabReferenceSet).Assembly,
        };
        _references = PrefabReferenceSet.ToPaths(PrefabReferenceSet.Select(seeds));
    }

    /// <summary>Executes verification across the specified prefab collection in compilation batches.</summary>
    public List<OracleResult> Run(IReadOnlyList<string> prefabNames, int batchSize = 32)
    {
        var results = new List<OracleResult>();
        for (var start = 0; start < prefabNames.Count; start += batchSize)
            results.AddRange(RunBatch([.. prefabNames.Skip(start).Take(batchSize)]));
        return results;
    }

    private List<OracleResult> RunBatch(IReadOnlyList<string> prefabNames)
    {
        var results = new List<OracleResult>();
        var generated = new List<(string Prefab, string Namespace, List<GeneratedSource> Sources)>();
        foreach (var prefab in prefabNames)
        {
            OracleMemoryGuard.Check($"generating '{prefab}'");
            // Assigns an isolated namespace per prefab so multiple generated creators coexist in a single assembly.
            var ns = $"{Namespace}.P{System.Threading.Interlocked.Increment(ref _batch)}";
            try
            {
                generated.Add((prefab, ns, Generate(prefab, ns)));
            }
            catch (Exception e)
            {
                results.Add(OracleDeclines.Classify(prefab, e, () =>
                {
                    BuildFromXml(prefab);
                    return null;
                }));
            }
        }

        OracleMemoryGuard.At($"compiling the batch from '{prefabNames[0]}'");
        foreach (var (group, assembly, errors) in _compiler.Compile($"{Namespace}.Batch", generated, x => x.Sources, _references))
        {
            // Registers generated widget classes with WidgetInfo for base constructor type discovery.
            if (assembly is not null)
                _compiler.Environment.CreateCreator(assembly);
            foreach (var (prefab, ns, _) in group)
            {
                OracleMemoryGuard.Check($"building '{prefab}'");
                results.Add(assembly is null
                    ? new(prefab, OracleOutcome.NotCompiled, [], string.Join(Environment.NewLine, errors.Take(5)), 0, 0)
                    : Compare(prefab, assembly, ns));
            }
        }
        return results;
    }

    private List<GeneratedSource> Generate(string prefab, string ns)
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(_widgetFactory);
        var context = new PrefabCodeGenerator(ns, _widgetFactory, _spriteData, _brushFactory);
        context.AddMovie(prefab, "Oracle", null);
        List<GeneratedSource> sources = [.. context.GenerateInMemory().Select(x => new GeneratedSource(ns + "." + x.Key, x.Value))];
        // Emits generated source code files to disk when source dumping is requested.
        if (Environment.GetEnvironmentVariable("UIEXTENDEREX_ORACLE_DUMP_SOURCES") == "1")
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UIExtenderEx-oracle", "static-sources", prefab);
            System.IO.Directory.CreateDirectory(directory);
            foreach (var source in sources)
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory, source.FileName), source.Content);
        }
        return sources;
    }

    /// <summary>
    /// Maps property names intentionally excluded from snapshot comparison to documented explanations.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> IgnoredProperties = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Version"] = "The change counter of a brush's styles and layers: how often a value was assigned, not what it is. The " +
                      "values themselves are compared. A compiled prefab rebinding the widgets under a replaced child ViewModel " +
                      "that the XML one leaves alone (BindingLoaderOracle's stale child ViewModel) assigns the same values again, " +
                      "and only this counter shows it - PartyTroopTuple, under {TradeData} and back out through {..}.",
        ["Tag"] = "WidgetTemplate's constructor sets Tag to Guid.NewGuid() and CreateWidgets copies it onto the widget: a value " +
                  "made up per parse, for the prefab editor. TaleWorlds' own pre-compiled prefabs leave it null as well.",
    };

    private OracleResult Compare(string prefab, Assembly assembly, string ns)
    {
        var xmlAsserts = new List<string>();
        Widget? fromXml;
        Widget? fromXmlAgain = null;
        var controlAsserts = new List<string>();
        string? xmlFailure = null;
        using (RecordAsserts(xmlAsserts))
        {
            try
            {
                OracleMemoryGuard.At($"building '{prefab}' from XML");
                fromXml = BuildFromXml(prefab);
            }
            catch (Exception e)
            {
                fromXml = null;
                xmlFailure = Describe(e);
            }
        }
        if (fromXml is null)
            return new(prefab, OracleOutcome.XmlThrows, [], xmlFailure, xmlAsserts.Count, 0);

        // Instantiates a secondary XML control tree to filter out inherent property noise, such as constructor randomizations.
        try
        {
            using (RecordAsserts(controlAsserts))
                fromXmlAgain = BuildFromXml(prefab);
        }
        catch (Exception)
        {
            // Continues verification using the primary tree when secondary control instantiation fails.
        }

        var compiledAsserts = new List<string>();
        Widget compiled;
        using (RecordAsserts(compiledAsserts))
        {
            try
            {
                OracleMemoryGuard.At($"building '{prefab}' compiled");
                compiled = BuildCompiled(prefab, assembly, ns);
            }
            catch (Exception e)
            {
                return new(prefab, OracleOutcome.CompiledThrows, [], Describe(e), xmlAsserts.Count, compiledAsserts.Count);
            }
        }

        OracleMemoryGuard.At($"comparing the trees of '{prefab}'");
        var differences = WidgetSnapshot.Compare(fromXml, compiled, fromXmlAgain, VisibleType)
            .Where(x => !IgnoredProperties.ContainsKey(LastSegment(x.Key)))
            .Select(x => x.Line)
            .ToList();
        differences.AddRange(AssertDifferences(xmlAsserts, [.. compiledAsserts.Select(x => _compiler.AsVisible(x, assembly))], fromXmlAgain is null ? null : controlAsserts));
        var assertDetail = xmlAsserts.Count == compiledAsserts.Count ? null
            : $"asserts: XML [{string.Join(" | ", xmlAsserts.Take(3))}], compiled [{string.Join(" | ", compiledAsserts.Take(3))}]";
        return new(prefab, differences.Count == 0 ? OracleOutcome.Match : OracleOutcome.Differs, differences, assertDetail, xmlAsserts.Count, compiledAsserts.Count);
    }

    /// <summary>
    /// Identifies assertion discrepancies between XML and compiled instantiations against the control baseline.
    /// </summary>
    internal static IEnumerable<string> AssertDifferences(IReadOnlyList<string> xml, IReadOnlyList<string> compiled, IReadOnlyList<string>? control)
    {
        if (xml.Count != compiled.Count)
            yield break;
        for (var i = 0; i < xml.Count; i++)
        {
            if (xml[i] != compiled[i] && (control is null || (i < control.Count && control[i] == xml[i])))
                yield return $"assert {i}: XML '{xml[i]}', compiled '{compiled[i]}'";
        }
    }

    private Widget BuildFromXml(string prefab) =>
        _widgetFactory.GetCustomType(prefab).Instantiate(new WidgetCreationData(_context, _widgetFactory)).Widget;

    internal static string KeyOf(string difference) => difference.Substring(0, difference.IndexOf(": XML ", StringComparison.Ordinal));

    internal static string LastSegment(string key)
    {
        var dot = key.LastIndexOf('.');
        return dot < 0 ? key : key.Substring(dot + 1);
    }

    private Widget BuildCompiled(string prefab, Assembly assembly, string ns)
    {
        var className = GeneratedNaming.GetUsableName(prefab) + "__Oracle";
        var rootType = assembly.GetType($"{ns}.{className}", throwOnError: true)!;
        var root = (Widget) Activator.CreateInstance(rootType, _context)!;
        foreach (var step in new[] { "CreateWidgets", "SetIds", "SetAttributes" })
        {
            try
            {
                rootType.GetMethod(step, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!.Invoke(root, []);
            }
            catch (TargetInvocationException e) when (e.InnerException is { } inner)
            {
                throw new InvalidOperationException($"{step} threw {Describe(inner)}", inner);
            }
        }
        return root;
    }

    private Type VisibleType(Type type) => _compiler.VisibleType(type);

    /// <summary>Formats exception diagnostic details, extracting the innermost message and relevant user frames.</summary>
    internal static string Describe(Exception exception)
    {
        var inner = exception;
        while (inner.InnerException is { } next && (inner is TargetInvocationException || inner.Message.Contains(" threw ")))
            inner = next;
        var frames = (inner.StackTrace ?? "").Split('\n').Select(x => x.Trim())
            .Where(x => x.Length > 0 && !x.StartsWith("at System.", StringComparison.Ordinal))
            .Take(3);
        return $"{inner.GetType().Name}: {inner.Message} [{string.Join(" <- ", frames)}]";
    }

    /// <summary>Installs an assertion recorder intercepting <see cref="Debug.FailedAssert"/> calls into the target list.</summary>
    internal static IDisposable RecordAsserts(List<string> into)
    {
        var previous = (IDebugManager?) DebugManagerProperty.GetValue(null);
        var previousInto = _assertsInto;
        _assertsInto = into;
        DebugManagerProperty.SetValue(null, AssertRecorder.Value);
        return new Restore(() =>
        {
            // Clears accumulated mock calls on the substitute to prevent unbounded memory growth across steps.
            AssertRecorder.Value.ClearReceivedCalls();
            _assertsInto = previousInto;
            DebugManagerProperty.SetValue(null, previous);
        });
    }

    private static readonly PropertyInfo DebugManagerProperty =
        typeof(Debug).GetProperty(nameof(Debug.DebugManager), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static List<string>? _assertsInto;

    /// <summary>
    /// Provides a singleton mock <see cref="IDebugManager"/> instance that routes assertions to the active destination list.
    /// </summary>
    private static readonly Lazy<IDebugManager> AssertRecorder = new(() =>
    {
        var manager = Substitute.For<IDebugManager>();
        manager.When(x => x.Assert(Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()))
            .Do(x =>
            {
                if (!x.ArgAt<bool>(0))
                    _assertsInto?.Add(x.ArgAt<string>(1).Replace('\n', ' '));
            });
        return manager;
    });

    private sealed class Restore(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}