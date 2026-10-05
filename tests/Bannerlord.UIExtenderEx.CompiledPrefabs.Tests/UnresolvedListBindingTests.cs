using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Linq;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies dynamic member resolution for collection bindings where element types cannot be statically resolved during code generation.
/// <para>
/// When collection element types are not statically determinable (such as untyped or interface-typed lists), the code generator
/// targets <see cref="IMBBindingList"/> and emits dynamic member lookups on individual items, mirroring XML loader reflection
/// and enabling compiled prefabs for screens like <c>KingdomManagement</c>.
/// </para>
/// </summary>
public class UnresolvedListBindingTests
{
    private const string Movie = "UnresolvedListMovie";

    private const string Prefab = """
<Prefab>
  <Window><Widget><Children>
    <ListPanel Id="Items" DataSource="{Items}">
      <ItemTemplate>
        <Widget Id="Item" HoveredCursorState="@ItemText" />
      </ItemTemplate>
    </ListPanel>
  </Children></Widget></Window>
</Prefab>
""";

    private const string TemplatesMovie = "UnresolvedListTemplatesMovie";

    private const string TemplatesPrefab = """
<Prefab>
  <Window><Widget><Children>
    <ListPanel Id="Items" DataSource="{Items}">
      <ItemTemplate>
        <Widget Id="Default" HoveredCursorState="@ItemText" />
      </ItemTemplate>
      <ItemTemplate Type="First">
        <Widget Id="First" HoveredCursorState="@ItemText" />
      </ItemTemplate>
      <ItemTemplate Type="Last">
        <Widget Id="Last" HoveredCursorState="@ItemText" />
      </ItemTemplate>
    </ListPanel>
  </Children></Widget></Window>
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
        _workspace = new PrefabWorkspace((Movie, Prefab), (TemplatesMovie, TemplatesPrefab));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(UnresolvedListBindingTests));
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui.Dispose();
        _workspace.Dispose();
    }

    private string Generate() => string.Join(Environment.NewLine, _workspace.Generate(Movie, typeof(ListRootVM)).Select(x => x.Content));

    private CompiledMovie Build(string movie = Movie) => CompiledMovie.Build(_workspace, movie, typeof(ListRootVM), _ui);

    private static string[] ItemTexts(CompiledMovie movie) =>
    [
        .. movie.AllById("Item").Concat(movie.AllById("Default")).Concat(movie.AllById("First")).Concat(movie.AllById("Last"))
            .Select(x => x.HoveredCursorState),
    ];

    // ---------------------------------------------------------------- Code emission tests

    [Test]
    public void AnUnresolvedList_IsDeclaredAsTheInterfaceTheLoaderWalks()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("private global::TaleWorlds.Library.IMBBindingList _datasource_Root_Items;"));
        Assert.That(code, Does.Contain("DynamicMember.GetChild(_datasource_Root, \"Items\") as global::TaleWorlds.Library.IMBBindingList"));
    }

    /// <summary>
    /// Verifies that list elements accessed via untyped <see cref="IMBBindingList"/> indexers are cast to <see cref="ViewModel"/>
    /// prior to initializing child template contexts.
    /// </summary>
    [Test]
    public void AnUnresolvedListsElement_IsNarrowedBeforeItIsUsedAsADataSource()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("_datasource_Root_Items[i] as global::TaleWorlds.Library.ViewModel"));
    }

    [Test]
    public void ItemBindings_AreEmittedByName()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("DynamicMember.Get(_datasource_Root, \"ItemText\")"));
    }

    // ---------------------------------------------------------------- Runtime collection lifecycle tests

    [Test]
    public void TheListIsPopulatedWhenTheDataSourceIsAssigned()
    {
        var movie = Build();
        var viewModel = new ListDerivedVM();
        viewModel.Items.Add(new ListItemVM("one"));
        viewModel.Items.Add(new ListItemVM("two"));

        movie.SetDataSource(viewModel);

        Assert.That(ItemTexts(movie), Is.EqualTo(new[] { "one", "two" }));
    }

    [Test]
    public void ADeclaredTypeWithNoListAtAllBuildsNothing()
    {
        var movie = Build();

        movie.SetDataSource(new ListRootVM());

        Assert.That(ItemTexts(movie), Is.Empty);
    }

    [Test]
    public void AddingAnItemAddsAWidget()
    {
        var movie = Build();
        var viewModel = new ListDerivedVM();
        movie.SetDataSource(viewModel);

        viewModel.Items.Add(new ListItemVM("added"));

        Assert.That(ItemTexts(movie), Is.EqualTo(new[] { "added" }));
    }

    [Test]
    public void RemovingAnItemRemovesItsWidget()
    {
        var movie = Build();
        var viewModel = new ListDerivedVM();
        viewModel.Items.Add(new ListItemVM("one"));
        viewModel.Items.Add(new ListItemVM("two"));
        movie.SetDataSource(viewModel);

        viewModel.Items.RemoveAt(0);

        Assert.That(ItemTexts(movie), Is.EqualTo(new[] { "two" }));
    }

    [Test]
    public void ClearingTheListRemovesEveryWidget()
    {
        var movie = Build();
        var viewModel = new ListDerivedVM();
        viewModel.Items.Add(new ListItemVM("one"));
        viewModel.Items.Add(new ListItemVM("two"));
        movie.SetDataSource(viewModel);

        viewModel.Items.Clear();

        Assert.That(ItemTexts(movie), Is.Empty);
    }

    [Test]
    public void SortingTheListReordersTheWidgets()
    {
        var movie = Build();
        var viewModel = new ListDerivedVM();
        viewModel.Items.Add(new ListItemVM("b"));
        viewModel.Items.Add(new ListItemVM("a"));
        movie.SetDataSource(viewModel);

        viewModel.Items.Sort(new ItemTextComparer());

        Assert.That(ItemTexts(movie), Is.EqualTo(new[] { "a", "b" }));
    }

    /// <summary>
    /// Verifies that replacing the entire list reference triggers a property change notification that recreates all item widgets.
    /// </summary>
    [Test]
    public void ReplacingTheListRebuildsTheWidgets()
    {
        var movie = Build();
        var viewModel = new ListDerivedVM();
        viewModel.Items.Add(new ListItemVM("old"));
        movie.SetDataSource(viewModel);

        viewModel.Replace([new ListItemVM("new")]);

        Assert.That(ItemTexts(movie), Is.EqualTo(new[] { "new" }));
    }

    /// <summary>
    /// Verifies that mutations on a detached previous list instance do not propagate to active widgets.
    /// </summary>
    [Test]
    public void TheOldListStopsReachingTheWidgets()
    {
        var movie = Build();
        var viewModel = new ListDerivedVM();
        var old = viewModel.Items;
        old.Add(new ListItemVM("old"));
        movie.SetDataSource(viewModel);
        viewModel.Replace([new ListItemVM("new")]);

        old.Add(new ListItemVM("this must not be seen"));

        Assert.That(ItemTexts(movie), Is.EqualTo(new[] { "new" }));
    }

    [Test]
    public void ClearingTheDataSourceRemovesEveryWidget()
    {
        var movie = Build();
        var viewModel = new ListDerivedVM();
        viewModel.Items.Add(new ListItemVM("one"));
        movie.SetDataSource(viewModel);

        movie.SetDataSource(null);

        Assert.That(ItemTexts(movie), Is.Empty);
    }

    /// <summary>
    /// Verifies that destroying the data source detaches collection listeners while preserving the widget tree structure.
    /// </summary>
    [Test]
    public void DestroyingTheDataSourceStopsTheListFollowingIt()
    {
        var movie = Build();
        var viewModel = new ListDerivedVM();
        viewModel.Items.Add(new ListItemVM("one"));
        movie.SetDataSource(viewModel);

        movie.DestroyDataSource();
        viewModel.Items.Add(new ListItemVM("this must not be seen"));

        Assert.That(ItemTexts(movie), Is.EqualTo(new[] { "one" }));
    }

    // ---------------------------------------------------------------- Polymorphic and derived collection tests

    /// <summary>
    /// Verifies that lists declared strictly as <see cref="IMBBindingList"/> route through dynamic item binding paths.
    /// </summary>
    [Test]
    public void AnInterfaceTypedList_Works()
    {
        var movie = Build();
        var viewModel = new InterfaceListVM();
        viewModel.Items.Add(new ListItemVM("interface"));

        movie.SetDataSource(viewModel);

        Assert.That(ItemTexts(movie), Is.EqualTo(new[] { "interface" }));
    }

    /// <summary>
    /// Verifies that non-generic derived lists resolve their element types from generic base collections.
    /// </summary>
    [Test]
    public void ANonGenericDerivedList_Works()
    {
        var movie = Build();
        var viewModel = new NonGenericListVM();
        viewModel.Items.Add(new ListItemVM("non generic"));

        movie.SetDataSource(viewModel);

        Assert.That(ItemTexts(movie), Is.EqualTo(new[] { "non generic" }));
    }

    // ---------------------------------------------------------------- Positional item template tests

    [Test]
    public void ASingleItemTakesTheFirstTemplate()
    {
        var movie = Build(TemplatesMovie);
        var viewModel = new ListDerivedVM();
        viewModel.Items.Add(new ListItemVM("only"));

        movie.SetDataSource(viewModel);

        Assert.That(movie.AllById("First").Select(x => x.HoveredCursorState), Is.EqualTo(new[] { "only" }));
        Assert.That(movie.AllById("Last"), Is.Empty);
    }

    /// <summary>
    /// Verifies that appending items to a list using First/Last item templates correctly transitions preceding items
    /// to the default template, matching XML loader <c>AddItemToList</c> semantics.
    /// </summary>
    [Test]
    public void GrowingPastOneItemMovesTheLastTemplate()
    {
        var movie = Build(TemplatesMovie);
        var viewModel = new ListDerivedVM();
        viewModel.Items.Add(new ListItemVM("one"));
        viewModel.Items.Add(new ListItemVM("two"));
        viewModel.Items.Add(new ListItemVM("three"));

        movie.SetDataSource(viewModel);

        Assert.That(movie.AllById("First"), Is.Empty);
        Assert.That(movie.AllById("Default").Select(x => x.HoveredCursorState), Is.EqualTo(new[] { "one", "two" }));
        Assert.That(movie.AllById("Last").Select(x => x.HoveredCursorState), Is.EqualTo(new[] { "three" }));
    }

    [Test]
    public void AddingToAnEmptyListThenGrowingItKeepsTheTemplatesRight()
    {
        var movie = Build(TemplatesMovie);
        var viewModel = new ListDerivedVM();
        movie.SetDataSource(viewModel);

        viewModel.Items.Add(new ListItemVM("one"));
        Assert.That(movie.AllById("First").Select(x => x.HoveredCursorState), Is.EqualTo(new[] { "one" }));

        viewModel.Items.Add(new ListItemVM("two"));
        Assert.That(ItemTexts(movie), Is.EquivalentTo(new[] { "one", "two" }));
    }

    private sealed class ItemTextComparer : System.Collections.Generic.IComparer<ListItemVM>
    {
        public int Compare(ListItemVM x, ListItemVM y) => string.CompareOrdinal(x.ItemText, y.ItemText);
    }
}
