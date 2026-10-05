using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Measures and compares movie instantiation latency across three execution paths: standard XML loading,
/// statically resolved compiled C#, and dynamically resolved compiled C# using name-based lookups.
/// <para>
/// Executes each test path through <c>GauntletMovie.Load</c>, controlling XML versus compiled dispatch via
/// <c>doNotUseGeneratedPrefabs</c> and selecting compiled variants based on runtime data source types.
/// </para>
/// <para>
/// Evaluates dynamic fallback performance using heterogeneous list collections where derived item properties
/// are not statically declared on the list element type, exercising <see cref="DynamicMember"/> lookups.
/// </para>
/// <para>
/// Run with <c>dotnet test --filter FullyQualifiedName~CompiledVersusXmlBenchmarks</c> to view timing metrics.
/// </para>
/// </summary>
[Explicit("Timing measurements, not a correctness check.")]
public class CompiledVersusXmlBenchmarks
{
    private const int Warmup = 5;
    private const int Iterations = 50;

    private const string Movie = "LoadComparisonMovie";

    /// <summary>
    /// Constructs a representative benchmark prefab containing static widgets with attribute bindings and a repeating list panel.
    /// Omits <c>TextWidget</c> instances to focus measurement purely on widget instantiation and binding resolution rather than font rendering.
    /// </summary>
    private static string Prefab() => $$"""
<Prefab>
  <Window>
    <Widget Id="Root" IsVisible="@Flag" MarginLeft="@Number">
      <Children>
        {{string.Concat(Enumerable.Range(0, 12).Select(i => $"""<Widget Id="Static{i}" IsVisible="@Flag" MarginTop="@Number" MarginLeft="@Number" />"""))}}
        <ListPanel Id="Items" DataSource="{Items}">
          <ItemTemplate>
            <Widget IsVisible="@Flag" MarginLeft="@Number" MarginTop="@Number" MarginRight="@Number">
              <Children>
                <Widget IsVisible="@Flag" MarginLeft="@Number" />
                <Widget IsVisible="@Flag" MarginTop="@Number" />
              </Children>
            </Widget>
          </ItemTemplate>
        </ListPanel>
      </Children>
    </Widget>
  </Window>
</Prefab>
""";

    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private IDynamicMemberHost? _previousHost;


    [SetUp]
    public void SetUp()
    {
        // Install a dynamic member host to support cache versioning for name-based property and command lookups.
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui?.Dispose();
        _workspace?.Dispose();
    }

    [TestCase(0, TestName = "OpeningAMovie_NoListItems")]
    [TestCase(10, TestName = "OpeningAMovie_TenListItems")]
    [TestCase(50, TestName = "OpeningAMovie_FiftyListItems")]
    public void OpeningAMovie(int items)
    {
        var compiler = new RoslynCompiler();

        _workspace = new PrefabWorkspace((Movie, Prefab()));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(CompiledVersusXmlBenchmarks));
        var factory = _workspace.WidgetFactory;
        var report = new StringBuilder();

        // Register distinct variants per ViewModel type in the factory, matching runtime CompiledPrefabManager registration.
        var resolvedSize = Register(compiler!, factory, typeof(LoadBenchRootVM));
        var byNameSize = Register(compiler!, factory, typeof(LoadBenchUnresolvedRootVM));

        var resolved = new LoadBenchRootVM(items);
        var byName = new LoadBenchUnresolvedRootVM(items);

        // Warm up each loading path prior to timing to ensure JIT compilation does not skew comparative measurements.
        Assert.That(Open(factory, resolved, compiled: true), Is.Not.InstanceOf<GauntletMovie>(), "the resolved build was declined");
        Assert.That(Open(factory, byName, compiled: true), Is.Not.InstanceOf<GauntletMovie>(), "the by-name build was declined");
        Assert.That(Open(factory, resolved, compiled: false), Is.InstanceOf<GauntletMovie>(), "the XML path was not taken");
        AssertBothCompiledTreesCarryTheValues(factory, resolved, byName, items);

        // Retain opened movies across benchmark rounds to isolate instantiation costs from GC collections and teardown overhead.
        // Alternate invocation order across rounds to balance warm-up effects and mitigate clock drift.
        var opened = new List<IGauntletMovie>();
        var order = new (string Name, ViewModel Source, bool Compiled)[]
        {
            ("xml", resolved, false),
            ("compiled", resolved, true),
            ("compiled, by name", byName, true),
        };
        for (var round = 0; round < 3; round++)
        {
            // Invoke Enumerable.Reverse explicitly to avoid in-place Span reversals on modern .NET runtimes.
            foreach (var (name, source, compiled) in round % 2 == 0 ? order : Enumerable.Reverse(order))
                Measure(report, $"round {round}: {name}", () => opened.Add(Open(factory, source, compiled)));
        }

        report.AppendLine($"  {items} list items; {resolvedSize / 1024} KB of C# resolved, {byNameSize / 1024} KB by name");
        Report(report);
    }

    /// <summary>
    /// Generates, compiles and registers the variant for one data source type, and answers how much C# it took. Which
    /// of the two it is gets asserted, so a generator that quietly resolved everything cannot be measured as a second
    /// copy of the resolved row.
    /// </summary>
    private int Register(ICSharpCompiler compiler, WidgetFactory factory, Type viewModelType)
    {
        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(factory, viewModelType);
        var sources = _workspace.Generate(Movie, viewModelType).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var code = string.Join(Environment.NewLine, sources.Select(x => x.Content));
        Assert.That(code.Contains("DynamicMember.Get("), Is.EqualTo(viewModelType == typeof(LoadBenchUnresolvedRootVM)),
            $"test premise: only {nameof(LoadBenchUnresolvedRootVM)} binds its items by name");

        var result = compiler.Compile(CompiledPrefabManager.GetAssemblyName(Movie, viewModelType, "loadbench000000"), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
        return sources.Sum(x => x.Content.Length);
    }

    /// <summary>
    /// Asserts that both compiled widget trees produce identical bound property values across all list items.
    /// </summary>
    private void AssertBothCompiledTreesCarryTheValues(WidgetFactory factory, ViewModel resolved, ViewModel byName, int items)
    {
        if (items == 0)
            return;

        var fromResolved = ItemMargins(Open(factory, resolved, compiled: true));
        var fromByName = ItemMargins(Open(factory, byName, compiled: true));

        Assert.That(fromResolved.Distinct().Count(), Is.GreaterThan(1), "test premise: the items carry different values");
        Assert.That(fromByName, Is.EqualTo(fromResolved).AsCollection, "the by-name bindings did not land the same values");
    }

    /// <summary>
    /// Extracts the left margin values of all child widgets generated by the list panel.
    /// </summary>
    private static List<float> ItemMargins(IGauntletMovie movie)
    {
        var list = Find(movie.RootWidget, "Items") ?? throw new InvalidOperationException("no list panel in the tree");
        var margins = new List<float>();
        for (var i = 0; i < list.ChildCount; i++)
            margins.Add(list.GetChild(i).MarginLeft);
        return margins;
    }

    private static Widget? Find(Widget? widget, string id)
    {
        if (widget is null)
            return null;
        if (widget.Id == id)
            return widget;
        for (var i = 0; i < widget.ChildCount; i++)
        {
            if (Find(widget.GetChild(i), id) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Loads the movie through <see cref="GauntletMovie.Load"/> using the specified compiled flag.
    /// </summary>
    private IGauntletMovie Open(WidgetFactory factory, ViewModel source, bool compiled) =>
        GauntletMovie.Load(_ui.Context, factory, Movie, source, doNotUseGeneratedPrefabs: !compiled, hotReloadEnabled: false);

    private static void Measure(StringBuilder report, string name, Action action)
    {
        for (var i = 0; i < Warmup; i++)
            action();

        var before = GC.GetTotalMemory(true);
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
            action();
        stopwatch.Stop();
        var allocated = GC.GetTotalMemory(false) - before;

        report.AppendLine($"  {name,-32} {stopwatch.Elapsed.TotalMilliseconds / Iterations,8:F3} ms per open   {allocated / (double) Iterations / 1024,8:F1} KB/open");
    }

    private static void Report(StringBuilder report) => NUnit.Framework.TestContext.Progress.WriteLine(Environment.NewLine + report);
}

/// <summary>
/// Represents a statically typed list item declaring bound properties directly.
/// </summary>
public class LoadBenchItemVM : ViewModel
{
    public LoadBenchItemVM(int index) => Number = index;

    [DataSourceProperty]
    public bool Flag => true;

    [DataSourceProperty]
    public int Number { get; }
}

/// <summary>
/// Serves as the declared base element type for unresolved list benchmarks, intentionally omitting template-bound members.
/// </summary>
public class LoadBenchBaseItemVM : ViewModel;

/// <summary>
/// Provides concrete item instances with properties resolved dynamically at runtime.
/// </summary>
public class LoadBenchDerivedItemVM : LoadBenchBaseItemVM
{
    public LoadBenchDerivedItemVM(int index) => Number = index;

    [DataSourceProperty]
    public bool Flag => true;

    [DataSourceProperty]
    public int Number { get; }
}

/// <summary>
/// Provides root data source properties and a typed list collection for statically resolved benchmarks.
/// </summary>
public class LoadBenchRootVM : ViewModel
{
    public LoadBenchRootVM(int items)
    {
        for (var i = 0; i < items; i++)
            Items.Add(new LoadBenchItemVM(i));
    }

    [DataSourceProperty]
    public bool Flag => true;

    [DataSourceProperty]
    public int Number => 4;

    [DataSourceProperty]
    public MBBindingList<LoadBenchItemVM> Items { get; } = [];
}

/// <summary>
/// Provides root data source properties with a base-typed list containing derived items for dynamic benchmark tests.
/// </summary>
public class LoadBenchUnresolvedRootVM : ViewModel
{
    public LoadBenchUnresolvedRootVM(int items)
    {
        for (var i = 0; i < items; i++)
            Items.Add(new LoadBenchDerivedItemVM(i));
    }

    [DataSourceProperty]
    public bool Flag => true;

    [DataSourceProperty]
    public int Number => 4;

    [DataSourceProperty]
    public MBBindingList<LoadBenchBaseItemVM> Items { get; } = [];
}
