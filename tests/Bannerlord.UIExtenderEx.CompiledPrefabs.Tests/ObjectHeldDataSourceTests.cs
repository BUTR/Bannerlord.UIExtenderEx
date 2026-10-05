using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies binding and tree construction semantics for data sources declared as untyped <see cref="object"/> references.
/// <para>
/// Ensures both XML and compiled prefabs successfully resolve dynamic member bindings and indexed list templates
/// when properties are typed as <see cref="object"/> rather than concrete <see cref="ViewModel"/> or <see cref="MBBindingList{T}"/> types.
/// </para>
/// </summary>
[NonParallelizable]
public class ObjectHeldDataSourceTests
{
    private const string Movie = "ObjectHeldMovie";
    private const string AssemblyTag = "objectheld0";

    private sealed class ProbeRuntime : Bannerlord.UIExtenderEx.Runtimes.IPrefabRuntime
    {
        public static readonly ProbeRuntime Instance = new();

        public bool TryServe(TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory widgetFactory, string movieName, IViewModel? dataSource) =>
            movieName == Movie && dataSource is ObjectHeldOwnerVM;

        public bool IsOwnVariant(Assembly variantAssembly) => variantAssembly.GetName().Name?.Contains(AssemblyTag) == true;
    }

    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private IDynamicMemberHost? _previousHost;

    [SetUp]
    public void SetUp()
    {
        Bannerlord.UIExtenderEx.Runtimes.PrefabRuntimes.Register(ProbeRuntime.Instance);
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        _workspace = new PrefabWorkspace((Movie, """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="Child" DataSource="{Child}" HoveredCursorState="@Name" />
        <Widget Id="First" DataSource="{Items\0}" HoveredCursorState="@Name" />
        <ListPanel Id="Held" DataSource="{Items}">
          <ItemTemplate><Widget HoveredCursorState="@Name" /></ItemTemplate>
        </ListPanel>
      </Children>
    </Widget>
  </Window>
</Prefab>
"""));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(ObjectHeldDataSourceTests));
        TaleWorlds.GauntletUI.GamepadNavigation.GauntletGamepadNavigationManager.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui?.Dispose();
        _workspace?.Dispose();
    }

    /// <summary>
    /// Verifies that a child property declared as <see cref="object"/> binds its child properties dynamically.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AChildDeclaredObject_IsBound(bool compiled)
    {
        var (_, root) = Open(compiled);

        Assert.That(root.FindChild("Child", includeAllChildren: true)!.HoveredCursorState, Is.EqualTo("child"));
    }

    /// <summary>
    /// Verifies that a list property declared as <see cref="object"/> resolves indexed bindings and populates item templates.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AListDeclaredObject_IsSteppedIntoAndBuilt(bool compiled)
    {
        var (_, root) = Open(compiled);

        Assert.That(root.FindChild("First", includeAllChildren: true)!.HoveredCursorState, Is.EqualTo("a"));
        Assert.That(root.FindChild("Held", includeAllChildren: true)!.ChildCount, Is.EqualTo(2));
    }

    private (ObjectHeldOwnerVM ViewModel, Widget Root) Open(bool compiled)
    {
        if (compiled)
            Register();
        var viewModel = new ObjectHeldOwnerVM();
        var movie = GauntletMovie.Load(_ui.Context, _workspace.WidgetFactory, Movie, viewModel, doNotUseGeneratedPrefabs: !compiled, hotReloadEnabled: false);
        Assert.That(movie is GauntletMovie, Is.EqualTo(!compiled), compiled ? "the compiled build was not used" : "the XML path was not taken");
        return (viewModel, movie.RootWidget);
    }

    private void Register()
    {
        var factory = _workspace.WidgetFactory;
        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(factory, typeof(ObjectHeldOwnerVM));
        var sources = _workspace.Generate(Movie, typeof(ObjectHeldOwnerVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = new RoslynCompiler().Compile(CompiledPrefabManager.GetAssemblyName(Movie, typeof(ObjectHeldOwnerVM), AssemblyTag), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
        var creator = environment.CreateCreator(environment.LoadAssembly(result.Assembly!));
        Assert.That(creator, Is.Not.Null);
        creator!(factory.GeneratedPrefabContext);
    }
}

public sealed class ObjectHeldOwnerVM : ViewModel
{
    [DataSourceProperty]
    public object Child { get; } = new ObjectHeldItemVM("child");

    [DataSourceProperty]
    public object Items { get; } = new MBBindingList<ObjectHeldItemVM> { new("a"), new("b") };

}

public sealed class ObjectHeldItemVM(string name) : ViewModel
{
    [DataSourceProperty]
    public string Name { get; } = name;
}
