using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Measures execution latency and allocation overhead across dynamic member fallback lookups and typed property accesses.
/// <para>
/// Run with <c>dotnet test --filter FullyQualifiedName~DynamicMemberBenchmarks</c> to view performance timings.
/// </para>
/// </summary>
[Explicit("Timing measurements, not a correctness check.")]
public class DynamicMemberBenchmarks
{
    private const int Warmup = 1_000;
    private const int Iterations = 200_000;

    private IDynamicMemberHost? _previousHost;
    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;

    [SetUp]
    public void SetUp()
    {
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        DynamicMember.Reset();
        _workspace = new PrefabWorkspace();
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(DynamicMemberBenchmarks));
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        DynamicMember.Reset();
        _ui.Dispose();
        foreach (var workspace in _fixtureWorkspaces)
            workspace.Dispose();
        _fixtureWorkspaces.Clear();
        _workspace.Dispose();
    }

    /// <summary>
    /// Measures constituent phases of dynamic member lookups: property table membership testing and <see cref="TaleWorlds.Library.ViewModel.GetPropertyValue"/> invocation.
    /// </summary>
    [Test]
    public void TheStepsOfADynamicRead()
    {
        var viewModel = new LoaderBaseVM();
        var host = new DynamicMemberHost();
        var report = new StringBuilder();

        Measure(report, "select the member off the table", () => _ = host.HasProperty(viewModel, "BaseText"));
        Measure(report, "ViewModel.GetPropertyValue", () => _ = viewModel.GetPropertyValue("BaseText"));

        Report(report);
    }

    /// <summary>
    /// Compares scalar property access latency across direct typed access, <see cref="DynamicMember.Get"/>, and raw <see cref="TaleWorlds.Library.ViewModel.GetPropertyValue"/>.
    /// </summary>
    [Test]
    public void ReadingAScalar()
    {
        var viewModel = new LoaderBaseVM();
        var report = new StringBuilder();

        Measure(report, "direct typed access", () => _ = viewModel.BaseText);
        Measure(report, "DynamicMember.Get", () => _ = DynamicMember.Get(viewModel, "BaseText"));
        Measure(report, "ViewModel.GetPropertyValue", () => _ = viewModel.GetPropertyValue("BaseText"));

        Report(report);
    }

    /// <summary>
    /// Measures dynamic read latency across four distinct lookup outcomes: standard values, dynamically registered properties, missing members, and null values.
    /// </summary>
    [Test]
    public void ReadingEachKindOfAnswer()
    {
        var viewModel = new LoaderBaseVM();
        var mixin = new LoaderBaseVM();
        mixin.AddProperty("Registered", new Bannerlord.BUTR.Shared.Utils.WrappedPropertyInfo(
            typeof(LoaderRegisteredMemberSource).GetProperty(nameof(LoaderRegisteredMemberSource.Value))!,
            new LoaderRegisteredMemberSource("value")));
        var report = new StringBuilder();

        Measure(report, "ordinary member with a value", () => _ = DynamicMember.Get(viewModel, "BaseText"));
        Measure(report, "registered member with a value", () => _ = DynamicMember.Get(mixin, "Registered"));
        Measure(report, "missing member, miss reported", () => _ = DynamicMember.Get(viewModel, "NoSuchProperty"));
        Measure(report, "present member answering null", () => _ = DynamicMember.Get(viewModel, "NullText"));

        Report(report);
    }

    /// <summary>
    /// Measures the performance cost of copy-on-write dynamic member registration across single and batched operations.
    /// </summary>
    [Test]
    public void RegisteringMembersOnViewModels()
    {
        var report = new StringBuilder();
        var property = typeof(LoaderRegisteredMemberSource).GetProperty(nameof(LoaderRegisteredMemberSource.Value))!;
        var source = new LoaderRegisteredMemberSource("value");
        PropertyInfo Wrapped() => new Bannerlord.BUTR.Shared.Utils.WrappedPropertyInfo(property, source);

        foreach (var count in new[] { 1, 4, 12 })
        {
            Measure(report, $"{count} separate registrations", () =>
            {
                var viewModel = new LoaderBaseVM();
                for (var i = 0; i < count; i++)
                    viewModel.AddProperty("Registered" + i, Wrapped());
            }, iterations: 20_000);
            Measure(report, $"{count} batched registrations", () =>
            {
                var viewModel = new LoaderBaseVM();
                using var registration = viewModel.BeginRegistration();
                for (var i = 0; i < count; i++)
                    registration.AddProperty("Registered" + i, Wrapped());
            }, iterations: 20_000);
        }
        Measure(report, "a bare ViewModel, for comparison", () => _ = new LoaderBaseVM(), iterations: 20_000);

        Report(report);
    }

    /// <summary>
    /// Measures execution latency for dynamic property writeback and command invocation against underlying ViewModel methods.
    /// </summary>
    [Test]
    public void WritingAndExecuting()
    {
        var viewModel = new LoaderBaseVM();
        var report = new StringBuilder();

        Measure(report, "DynamicMember.Set", () => DynamicMember.Set(viewModel, "WritableText", "x"));
        Measure(report, "ViewModel.SetPropertyValue", () => viewModel.SetPropertyValue("WritableText", "x"));
        Measure(report, "DynamicMember.Execute", () => DynamicMember.Execute(viewModel, "ExecuteNoArguments", []));
        Measure(report, "ViewModel.ExecuteCommand", () => viewModel.ExecuteCommand("ExecuteNoArguments", []));

        Report(report);
    }

    /// <summary>
    /// Measures end-to-end access latency combining member reads and widget property assignment across typed and dynamic paths.
    /// </summary>
    [Test]
    public void ReadingAScalarAndAssigningItToAWidget()
    {
        var viewModel = new LoaderBaseVM();
        var widget = new Widget(_ui.Context);
        var report = new StringBuilder();

        Measure(report, "typed access + typed assignment", () => widget.Id = viewModel.BaseText);
        // Measure the guarded typed assignment pattern emitted for eligible name-based reads.
        Measure(report, "DynamicMember.Get + guarded typed assignment", () =>
        {
            var read = DynamicMember.Get(viewModel, "BaseText");
            if (read is string)
            {
                var assigned = (string) read;
                try
                {
                    widget.Id = assigned;
                }
                catch (Exception failure)
                {
                    throw new TargetInvocationException(failure);
                }
            }
            else
            {
                WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "Id", read);
            }
        });
        // Measure the fallback SetWidgetAttribute pattern emitted when fast path eligibility checks decline.
        Measure(report, "DynamicMember.Get + SetWidgetAttribute",
            () => WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "Id", DynamicMember.Get(viewModel, "BaseText")));
        Measure(report, "GetPropertyValue + SetWidgetAttribute",
            () => WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "Id", viewModel.GetPropertyValue("BaseText")));

        Report(report);
    }

    // ---------------------------------------------------------------- the notification path

    /// <summary>
    /// Specifies a test prefab declaring a single boolean binding.
    /// </summary>
    private const string OneBoolPrefab = """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="Target" IsVisible="@Flag" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""";

    /// <summary>
    /// Specifies a test prefab declaring four identical boolean bindings to simulate fan-out property dispatch.
    /// </summary>
    private const string FourBoolsPrefab = """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="A" IsVisible="@Flag" />
        <Widget Id="B" IsVisible="@Flag" />
        <Widget Id="C" IsVisible="@Flag" />
        <Widget Id="D" IsVisible="@Flag" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""";

    /// <summary>
    /// Specifies a test prefab binding an integer source property to a floating-point widget attribute.
    /// </summary>
    private const string WideningPrefab = """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="Target" MarginLeft="@Ratio" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""";

    /// <summary>
    /// Tracks active workspaces for generated benchmark fixtures to preserve on-disk prefab definitions throughout test execution.
    /// </summary>
    private readonly List<PrefabWorkspace> _fixtureWorkspaces = [];

    private BenchSourceVM Fixture(string name, string prefab, Type generateAgainst)
    {
        var workspace = new PrefabWorkspace((name, prefab));
        _fixtureWorkspaces.Add(workspace);
        var movie = CompiledMovie.Build(workspace, name, generateAgainst, _ui);
        var source = new BenchSourceVM();
        movie.SetDataSource(source);
        return source;
    }

    /// <summary>
    /// Compares property change notification performance between statically resolved bindings and dynamic by-name bindings.
    /// <para>
    /// Alternates boolean states on each cycle to ensure property setters execute complete layout invalidation and writeback cycles.
    /// </para>
    /// </summary>
    [Test]
    public void NotifyingAScalarChange()
    {
        var byName = Fixture("BenchByName", OneBoolPrefab, typeof(BenchRootVM));
        var resolved = Fixture("BenchResolved", OneBoolPrefab, typeof(BenchSourceVM));
        var report = new StringBuilder();
        var toggle = false;

        Measure(report, "resolved binding, typed notification", () => resolved.AnnounceFlag(toggle = !toggle));
        Measure(report, "by-name binding, typed notification", () => byName.AnnounceFlag(toggle = !toggle));
        Measure(report, "resolved binding, ordinary notification", () => resolved.SetFlagAndAnnouncePlainly(toggle = !toggle));
        Measure(report, "by-name binding, ordinary notification", () => byName.SetFlagAndAnnouncePlainly(toggle = !toggle));

        report.AppendLine($"  final state: resolved {resolved.Flag}, by-name {byName.Flag}");
        Report(report);
    }

    /// <summary>
    /// Measures property notification overhead when the bound property name does not exist on the target data source.
    /// </summary>
    [Test]
    public void NotifyingAChangeNothingAnswers()
    {
        var workspace = new PrefabWorkspace(("BenchMissing", OneBoolPrefab));
        _fixtureWorkspaces.Add(workspace);
        var movie = CompiledMovie.Build(workspace, "BenchMissing", typeof(BenchRootVM), _ui);
        var source = new BenchEmptyVM();
        movie.SetDataSource(source);
        var report = new StringBuilder();

        Measure(report, "missing member", () => source.AnnouncePlain("Flag"));

        Report(report);
    }

    /// <summary>
    /// Measures property notification latency across four widget subscribers bound to the same property.
    /// </summary>
    [Test]
    public void NotifyingAScalarChangeToFourWidgets()
    {
        var byName = Fixture("BenchByNameFour", FourBoolsPrefab, typeof(BenchRootVM));
        var resolved = Fixture("BenchResolvedFour", FourBoolsPrefab, typeof(BenchSourceVM));
        var report = new StringBuilder();
        var toggle = false;

        Measure(report, "resolved bindings x4, typed notification", () => resolved.AnnounceFlag(toggle = !toggle));
        Measure(report, "by-name bindings x4, typed notification", () => byName.AnnounceFlag(toggle = !toggle));
        Measure(report, "by-name bindings x4, ordinary notification", () => byName.SetFlagAndAnnouncePlainly(toggle = !toggle));

        Report(report);
    }

    /// <summary>
    /// Measures notification latency for primitive numeric widening across typed and untyped object notification paths.
    /// </summary>
    [Test]
    public void NotifyingAWideningChange()
    {
        var byName = Fixture("BenchWidening", WideningPrefab, typeof(BenchRootVM));
        var resolved = Fixture("BenchWideningResolved", WideningPrefab, typeof(BenchSourceVM));
        var report = new StringBuilder();
        var value = 0;

        Measure(report, "resolved binding, typed notification", () => resolved.AnnounceRatioAsInt(value ^= 1));
        Measure(report, "by-name binding, typed notification", () => byName.AnnounceRatioAsInt(value ^= 1));
        Measure(report, "by-name binding, object notification", () => byName.AnnounceRatioAsObject(value ^= 1));

        Report(report);
    }

    /// <summary>
    /// First-open cost, which removing the accessor compiler was meant not to have made worse and seven more overloads
    /// could have. Generation, compilation and the first attach, measured once each because that is how often they happen.
    /// </summary>
    [Test]
    public void OpeningAMovieForTheFirstTime()
    {
        var report = new StringBuilder();

        foreach (var (label, type) in new[] { ("byname", typeof(BenchRootVM)), ("resolved", typeof(BenchSourceVM)) })
        {
            var name = "BenchCold" + label;
            using var workspace = new PrefabWorkspace((name, FourBoolsPrefab));

            var generation = Stopwatch.StartNew();
            var sources = workspace.Generate(name, type);
            generation.Stop();
            var characters = 0;
            foreach (var source in sources)
                characters += source.Content.Length;

            var everything = Stopwatch.StartNew();
            var movie = CompiledMovie.Build(workspace, name, type, _ui);
            movie.SetDataSource(new BenchSourceVM());
            everything.Stop();

            report.AppendLine($"  {label,-10} generate {generation.Elapsed.TotalMilliseconds,8:F2} ms   "
                + $"generate+compile+attach {everything.Elapsed.TotalMilliseconds,8:F2} ms   {characters,7} chars emitted");
        }

        Report(report);
    }

    /// <summary>Two runtime types down one binding, which is the polymorphic data source the fallback exists for.</summary>
    [Test]
    public void ReadingAlternatingRuntimeTypes()
    {
        var first = new LoaderDerivedVM();
        var second = new LoaderOtherDerivedVM();
        var report = new StringBuilder();
        var toggle = false;

        Measure(report, "DynamicMember.Get, alternating subclasses", () =>
        {
            toggle = !toggle;
            _ = DynamicMember.Get(toggle ? first : (LoaderBaseVM) second, "DerivedText");
        });
        Measure(report, "GetPropertyValue, alternating subclasses", () =>
        {
            toggle = !toggle;
            _ = (toggle ? first : (LoaderBaseVM) second).GetPropertyValue("DerivedText");
        });

        Report(report);
    }

    /// <summary>
    /// The ways a mixin member can be read, before anyone claims the resolved mixin path is the fast one.
    /// <c>ViewModelMixins.Get&lt;T&gt;</c> is a lookup of its own: every registered runtime, a weak-table probe per
    /// runtime, then a scan of that instance's mixin list. A <em>resolved</em> mixin binding used to emit it on every read;
    /// it now emits the data source field's mixin receiver, which pays for it once per data source. Beside them is the
    /// same value read by name.
    /// <para>
    /// Measured 2026-09-23, net472 Release: <c>Get</c> 149 ns and 30 B while the runtime list was a LINQ query, 16 ns
    /// and nothing once it was a published array; the receiver 1.4 ns; by name 81 ns.
    /// </para>
    /// </summary>
    [Test]
    public void ReadingAMixinMemberBothWays()
    {
        var extender = UIExtender.Create("TestModule.CompiledPrefabs.MixinBenchmark");
        extender.Register([typeof(MixinInstanceMixin)]);
        extender.Enable();
        try
        {
            var viewModel = new MixinInstanceVM("value");
            var report = new StringBuilder();

            Measure(report, "ViewModelMixins.Get<TMixin>", () => _ = ViewModels.ViewModelMixins.Get<MixinInstanceMixin>(viewModel));
            Measure(report, "ViewModelMixins.Get + member read",
                () => _ = ViewModels.ViewModelMixins.Get<MixinInstanceMixin>(viewModel)?.InstanceText);
            Measure(report, "DynamicMember.Get, by name", () => _ = DynamicMember.Get(viewModel, "InstanceText"));

            // What a compiled prefab emits for a resolved mixin member: the field's receiver, which looks the instance up
            // only when the field holds a different ViewModel than last time
            TaleWorlds.Library.ViewModel? owner = null;
            MixinInstanceMixin? receiver = null;
            Measure(report, "mixin receiver + member read", () =>
            {
                if (!ReferenceEquals(viewModel, owner))
                {
                    owner = viewModel;
                    receiver = ViewModels.ViewModelMixins.Get<MixinInstanceMixin>(viewModel);
                }
                _ = receiver?.InstanceText;
            });

            Report(report);
        }
        finally
        {
            extender.Deregister();
        }
    }

    /// <summary>A member registered per instance, where the receiver has to be resolved on every access.</summary>
    [Test]
    public void ReadingARegisteredMember()
    {
        var viewModel = new LoaderBaseVM();
        viewModel.AddProperty("Registered", new Bannerlord.BUTR.Shared.Utils.WrappedPropertyInfo(
            typeof(LoaderRegisteredMemberSource).GetProperty(nameof(LoaderRegisteredMemberSource.Value))!,
            new LoaderRegisteredMemberSource("value")));
        var report = new StringBuilder();

        Measure(report, "DynamicMember.Get, registered member", () => _ = DynamicMember.Get(viewModel, "Registered"));
        Measure(report, "GetPropertyValue, registered member", () => _ = viewModel.GetPropertyValue("Registered"));

        Report(report);
    }

    private static void Measure(StringBuilder report, string name, Action action, int iterations = Iterations)
    {
        for (var i = 0; i < Warmup; i++)
            action();

        var before = GC.GetTotalMemory(true);
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            action();
        stopwatch.Stop();
        var allocated = GC.GetTotalMemory(false) - before;

        report.AppendLine($"  {name,-42} {stopwatch.Elapsed.TotalMilliseconds * 1_000_000 / iterations,9:F1} ns   {allocated / (double) iterations,7:F1} B/op");
    }

    private static void Report(StringBuilder report) => TestContext.Progress.WriteLine(Environment.NewLine + report);
}
