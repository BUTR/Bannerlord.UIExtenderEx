using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests dynamic member fallback for child <c>DataSource</c> scope paths that cannot be statically resolved on declared ViewModel types.
/// <para>
/// Instead of aborting code generation when child data source paths diverge from static type declarations, the generator
/// emits general <see cref="ViewModel"/> scope fields populated at runtime via <see cref="DynamicMember.GetChild"/> and <see cref="DynamicMember.Step"/>.
/// Child widgets within the scope bind dynamically while matching XML loader instance traversal.
/// </para>
/// </summary>
public class UnresolvedDataSourcePathTests
{
    private const string Movie = "UnresolvedScopeMovie";

    private const string Prefab = """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget DataSource="{Known}"><Children>
          <Widget Id="Known" HoveredCursorState="@LeafText" />
        </Children></Widget>
        <Widget DataSource="{Extra}"><Children>
          <Widget Id="Branch" HoveredCursorState="@BranchText" />
          <Widget DataSource="{Deeper}"><Children>
            <Widget Id="Deep" HoveredCursorState="@LeafText" />
          </Children></Widget>
        </Children></Widget>
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
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(UnresolvedDataSourcePathTests));
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui.Dispose();
        _workspace.Dispose();
    }

    private string Generate() => string.Join("\n", _workspace.Generate(Movie, typeof(ScopeRootVM)).Select(x => x.Content));

    private CompiledMovie Build() => CompiledMovie.Build(_workspace, Movie, typeof(ScopeRootVM), _ui);

    // ---------------------------------------------------------------- Code emission tests

    [Test]
    public void AnUnresolvedScope_IsDeclaredAsAViewModel()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("private global::TaleWorlds.Library.ViewModel _datasource_Root_Extra;"));
        Assert.That(code, Does.Contain("private global::TaleWorlds.Library.ViewModel _datasource_Root_Extra_Deeper;"),
            "and so is everything under it");
    }

    [Test]
    public void AResolvedScope_KeepsItsDeclaredType()
    {
        var code = Generate();

        Assert.That(code, Does.Contain($"private global::{typeof(ScopeLeafVM).FullName} _datasource_Root_Known;"));
    }

    [Test]
    public void AnUnresolvedScope_IsFilledByName()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("_datasource_Root_Extra_object = global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.GetChild(_datasource_Root, \"Extra\");"));
        // Traverses child scope properties on dynamic instances via DynamicMember.Step.
        Assert.That(code, Does.Contain("_datasource_Root_Extra_Deeper_object = global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.Step(_datasource_Root_Extra_object, \"Deeper\");"));
    }

    [Test]
    public void AResolvedScope_IsStillReachedTyped()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("_datasource_Root_Known = _datasource_Root.Known;"));
    }

    // ---------------------------------------------------------------- Runtime binding execution tests

    [Test]
    public void BindingsUnderAnUnresolvedScope_Work()
    {
        var movie = Build();

        movie.SetDataSource(new ScopeDerivedVM());

        Assert.That(movie.ById("Known").HoveredCursorState, Is.EqualTo("known"), "the resolved scope still works");
        Assert.That(movie.ById("Branch").HoveredCursorState, Is.EqualTo("first"));
        Assert.That(movie.ById("Deep").HoveredCursorState, Is.EqualTo("deep"), "and so does the scope below it");
    }

    /// <summary>
    /// Verifies that absent child scopes on the runtime instance leave downstream bindings unaffected without throwing exceptions.
    /// </summary>
    [Test]
    public void AnAbsentScopeLeavesEverythingUnderItAlone()
    {
        var movie = Build();

        movie.SetDataSource(new ScopeRootVM());

        Assert.That(movie.ById("Branch").HoveredCursorState, Is.Null);
        Assert.That(movie.ById("Deep").HoveredCursorState, Is.Null);
    }

    /// <summary>
    /// Verifies that swapping polymorphic ViewModel instances dynamically rebinds unresolved child scopes.
    /// </summary>
    [Test]
    public void SwappingTheRuntimeTypeRebindsTheUnresolvedScope()
    {
        var movie = Build();

        movie.SetDataSource(new ScopeDerivedVM());
        Assert.That(movie.ById("Branch").HoveredCursorState, Is.EqualTo("first"));

        movie.SetDataSource(new ScopeOtherDerivedVM());
        Assert.That(movie.ById("Branch").HoveredCursorState, Is.EqualTo("other"));

        movie.SetDataSource(null);
        movie.SetDataSource(new ScopeDerivedVM());
        Assert.That(movie.ById("Branch").HoveredCursorState, Is.EqualTo("first"));
    }

    /// <summary>
    /// Verifies that property change notifications on the scope property itself trigger scope recreation and downstream rebinding.
    /// </summary>
    [Test]
    public void ReplacingTheScopeObjectRebindsEverythingUnderIt()
    {
        var movie = Build();
        var viewModel = new ScopeDerivedVM();
        movie.SetDataSource(viewModel);
        Assert.That(movie.ById("Branch").HoveredCursorState, Is.EqualTo("first"));

        viewModel.SetExtra(new ScopeBranchVM("second"));

        Assert.That(movie.ById("Branch").HoveredCursorState, Is.EqualTo("second"));
        Assert.That(movie.ById("Deep").HoveredCursorState, Is.EqualTo("deep"));
    }

    /// <summary>
    /// Verifies that property names serving simultaneously as child scopes and scalar bindings bypass typed payload handlers,
    /// ensuring scope re-evaluation handlers rebuild the nested hierarchy.
    /// </summary>
    [Test]
    public void ANameThatIsAlsoAChildScope_IsNotServedFromTheTypedPayload()
    {
        using var workspace = new PrefabWorkspace(("ScopeAndValueMovie", """
<Prefab><Window><Widget><Children>
  <Widget Id="AsValue" HoveredCursorState="@Extra" />
  <Widget DataSource="{Extra}"><Children>
    <Widget Id="AsScope" HoveredCursorState="@BranchText" />
  </Children></Widget>
</Children></Widget></Window></Prefab>
"""));

        var code = string.Join(Environment.NewLine, workspace.Generate("ScopeAndValueMovie", typeof(ScopeRootVM)).Select(x => x.Content));

        Assert.That(code, Does.Not.Contain("HandleViewModelPropertyChangeWithValueOf_datasource_Root("),
            "the only unresolved binding at this scope is a name that carries the scope, so there is nothing to serve from a payload");
        Assert.That(code, Does.Contain("RefreshDataSource_datasource_Root_Extra("), "and the scope is still rebuilt on the name");
    }

    [Test]
    public void ClearingTheScopeObjectStopsTheBindingsUnderIt()
    {
        var movie = Build();
        var viewModel = new ScopeDerivedVM();
        movie.SetDataSource(viewModel);

        viewModel.SetExtra(null);

        Assert.That(() => movie.ById("Branch").HoveredCursorState, Throws.Nothing);
    }

    // ---------------------------------------------------------------- Diagnostic and rejection tests

    /// <summary>
    /// Verifies that data source paths referencing non-publicly readable members are rejected during code generation,
    /// matching XML loader access restrictions.
    /// </summary>
    [Test]
    public void APathThroughANonPublicMember_IsStillDeclined()
    {
        using var workspace = new PrefabWorkspace(("NonPublicScopeMovie", """
<Prefab><Window><Widget><Children><Widget DataSource="{HiddenChild}"><Children>
  <Widget Id="Leaf" HoveredCursorState="@LeafText" />
</Children></Widget></Children></Widget></Window></Prefab>
"""));

        var failure = Assert.Throws<InvalidOperationException>(() => workspace.Generate("NonPublicScopeMovie", typeof(HiddenScopeVM)));

        Assert.That(failure!.Message, Does.Contain("HiddenChild"));
        Assert.That(failure.Message, Does.Contain("not publicly readable"));
    }

    /// <summary>
    /// Verifies that contradictory usage of an unresolved path as both a collection and an object scope triggers a compilation diagnostic.
    /// </summary>
    [Test]
    public void APathUsedAsBothAListAndAScope_IsDeclinedByName()
    {
        using var workspace = new PrefabWorkspace(("ContradictoryScopeMovie", """
<Prefab><Window><Widget><Children>
  <ListPanel DataSource="{Extra}">
    <ItemTemplate><Widget /></ItemTemplate>
  </ListPanel>
  <Widget DataSource="{Extra}"><Children>
    <Widget DataSource="{Deeper}"><Children><Widget Id="Deep" /></Children></Widget>
  </Children></Widget>
</Children></Widget></Window></Prefab>
"""));

        var failure = Assert.Throws<InvalidOperationException>(() => workspace.Generate("ContradictoryScopeMovie", typeof(ScopeRootVM)));

        Assert.That(failure!.Message, Does.Contain("Extra"));
        Assert.That(failure.Message, Does.Contain("both as a list and as the scope"));
    }
}
