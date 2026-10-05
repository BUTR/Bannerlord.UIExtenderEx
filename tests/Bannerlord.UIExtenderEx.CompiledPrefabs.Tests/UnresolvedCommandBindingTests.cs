using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies dynamic member resolution for command bindings that cannot be statically resolved on declared ViewModel types.
/// <para>
/// In TaleWorlds GauntletUI, <c>ViewModel.ExecuteCommand</c> inspects runtime instances across base classes and non-public methods.
/// When the code generator encounters a command unresolvable against the declared type, it emits runtime calls
/// via <see cref="DynamicMember.Execute"/>, preserving parity with the XML loader rather than dropping the invocation.
/// </para>
/// </summary>
public class UnresolvedCommandBindingTests
{
    private const string Movie = "UnresolvedCommandMovie";

    private const string Prefab = """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="Base" Command.Click="ExecuteBaseCommand" />
        <Widget Id="Subclass" Command.Click="ExecuteSubclassCommand" />
        <Widget Id="Private" Command.Click="ExecutePrivateCommand" />
        <Widget Id="Missing" Command.Click="NoSuchCommandAnywhere" />
        <Widget Id="Literal" Command.Click="ExecuteWithString" CommandParameter.Click="literal" />
        <Widget Id="IntLiteral" Command.Click="ExecuteWithInt" CommandParameter.Click="42" />
        <Widget Id="Incompatible" Command.Click="ExecuteWithInt" CommandParameter.Click="not a number" />
        <Widget Id="Source" Command.Click="ExecuteWithSource" />
        <Widget Id="ListArgument" Command.Click="ExecuteWithList" />
        <ListPanel Id="List" DataSource="{Items}">
          <ItemTemplate><Widget Id="Item" /></ItemTemplate>
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
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        _workspace = new PrefabWorkspace((Movie, Prefab));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(UnresolvedCommandBindingTests));
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui.Dispose();
        _workspace.Dispose();
    }

    private string Generate() => string.Join(Environment.NewLine, _workspace.Generate(Movie, typeof(CommandRootVM)).Select(x => x.Content));

    private CompiledMovie Build() => CompiledMovie.Build(_workspace, Movie, typeof(CommandRootVM), _ui);

    private static void Click(CompiledMovie movie, string id, params object[] arguments) =>
        CompiledMovie.FireEvent(movie.ById(id), "Click", arguments);

    // ---------------------------------------------------------------- Code emission tests

    [Test]
    public void ACommandTheDeclaredTypeHas_StaysATypedCall()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("_datasource_Root.ExecuteBaseCommand();"));
        Assert.That(code, Does.Not.Contain("\"ExecuteBaseCommand\""), "nothing the generator can resolve pays for a lookup");
    }

    [Test]
    public void ACommandOnlyTheSubclassHas_IsRunByName()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("DynamicMember.Execute(_datasource_Root, \"ExecuteSubclassCommand\", arguments)"));
        Assert.That(code, Does.Not.Contain("_datasource_Root.ExecuteSubclassCommand("), "never a typed call to a member that does not exist");
    }

    /// <summary>
    /// Verifies that prefab command parameters are emitted as raw strings, deferring type conversion to runtime invocation.
    /// </summary>
    [Test]
    public void ThePrefabParameterIsAppendedAsAString()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("var arguments = new global::System.Object[args.Length + 1];"));
        Assert.That(code, Does.Contain("arguments[args.Length] = \"literal\";"));
    }

    [Test]
    public void ACommandWithNoParameterTakesOnlyTheEventArguments()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("var arguments = new global::System.Object[args.Length];"));
    }

    // ---------------------------------------------------------------- Runtime command execution tests

    [Test]
    public void ACommandTheDeclaredTypeHas_Runs()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Base");

        Assert.That(viewModel.BaseCalls, Is.EqualTo(1));
    }

    [Test]
    public void ACommandOnlyTheSubclassHas_Runs()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Subclass");

        Assert.That(viewModel.Calls, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that zero-parameter command methods execute successfully even when event handlers supply surplus arguments.
    /// </summary>
    [Test]
    public void AZeroParameterCommandRunsWithExtraEventArguments()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Subclass", movie.ById("Subclass"), "extra");

        Assert.That(viewModel.Calls, Is.EqualTo(1));
    }

    [Test]
    public void APrivateCommandRuns()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Private");

        Assert.That(viewModel.Calls, Is.EqualTo(1));
    }

    [Test]
    public void ACommandNothingHasDoesNothing()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Assert.That(() => Click(movie, "Missing"), Throws.Nothing);
        Assert.That(viewModel.Calls, Is.Zero);
    }

    [Test]
    public void ACommandRunOnTheDeclaredTypeAloneDoesNothing()
    {
        var movie = Build();
        var viewModel = new CommandRootVM();
        movie.SetDataSource(viewModel);

        Assert.That(() => Click(movie, "Subclass"), Throws.Nothing);
    }

    [Test]
    public void ALiteralParameterReachesTheCommand()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Literal");

        Assert.That(viewModel.LastString, Is.EqualTo("literal"));
    }

    /// <summary>
    /// Verifies that string literal command parameters convert to target parameter types during runtime command execution.
    /// </summary>
    [Test]
    public void ALiteralParameterIsConvertedToTheParameterType()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Click(movie, "IntLiteral");

        Assert.That(viewModel.LastInt, Is.EqualTo(42));
    }

    /// <summary>
    /// Verifies that incompatible literal parameters throw conversion exceptions rather than silently suppressing command calls.
    /// </summary>
    [Test]
    public void AnIncompatibleLiteralFailsTheSameWayTheLoaderDoes()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Assert.That(() => Click(movie, "Incompatible"), Throws.Exception);
        Assert.That(viewModel.Calls, Is.Zero);
    }

    /// <summary>
    /// Verifies that widget arguments passed into commands convert to their bound ViewModel data source instances.
    /// </summary>
    [Test]
    public void AWidgetArgumentBecomesItsViewModel()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Source", movie.ById("Base"));

        Assert.That(viewModel.LastArgument, Is.SameAs(viewModel));
    }

    /// <summary>
    /// Verifies that widgets bound to collection data sources extract and provide the underlying binding list as the command argument.
    /// </summary>
    [Test]
    public void AWidgetBoundToAListBecomesTheList()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Click(movie, "ListArgument", movie.ById("List"));

        Assert.That(viewModel.LastArgument, Is.SameAs(viewModel.Items));
    }

    /// <summary>
    /// Verifies that unbound widgets without active data sources resolve to null command arguments.
    /// </summary>
    [Test]
    public void AnUnboundWidgetArgumentBecomesNull()
    {
        var movie = Build();
        var viewModel = new CommandDerivedVM();
        movie.SetDataSource(viewModel);

        Click(movie, "Source", new TaleWorlds.GauntletUI.BaseTypes.Widget(_ui.Context));

        Assert.That(viewModel.Calls, Is.EqualTo(1));
        Assert.That(viewModel.LastArgument, Is.Null);
    }
}
