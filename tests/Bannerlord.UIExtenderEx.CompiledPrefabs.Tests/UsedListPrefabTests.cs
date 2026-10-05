using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System.Collections.Generic;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies code generation and runtime evaluation for prefabs referencing external sub-prefabs whose root element
/// is a collection panel containing item templates (<c>&lt;ListPanel DataSource="{Items}"&gt;&lt;ItemTemplate&gt;</c>).
/// <para>
/// Ensures the compiled prefab compiler associates item templates defined on the sub-prefab's root element with the
/// consuming instantiation widget, matching XML loader <c>PrefabDatabindingExtension.OnWidgetCreated</c> behavior.
/// </para>
/// </summary>
[NonParallelizable]
public class UsedListPrefabTests
{
    private const string ListPart = """<Prefab><Window><ListPanel DataSource="{Items}"><ItemTemplate><LabelWidget Label="@Label" /></ItemTemplate></ListPanel></Window></Prefab>""";

    private IDynamicMemberHost? _previousHost;

    [SetUp]
    public void SetUp()
    {
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
    }

    [TearDown]
    public void TearDown() => DynamicMember.Host = _previousHost;

    private static List<string?> Texts(Widget root)
    {
        var texts = new List<string?>();
        Collect(root);
        return texts;

        void Collect(Widget widget)
        {
            if (widget is LabelWidget label)
                texts.Add(label.Label);
            for (var i = 0; i < widget.ChildCount; i++)
                Collect(widget.GetChild(i));
        }
    }

    /// <summary>
    /// Executes the specified movie across both XML loader and compiled runtimes, asserting visual element text equivalence before and after applying changes.
    /// </summary>
    private static void AssertSameTexts(string movie, (string, string)[] prefabs, string[] expected, System.Action<UsedListRootVM>? change = null, string[]? expectedAfter = null)
    {
        using var workspace = new PrefabWorkspace(prefabs);
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(UsedListPrefabTests));
        var xmlViewModel = new UsedListRootVM();
        var compiledViewModel = new UsedListRootVM();

        var xml = GauntletMovie.Load(ui.Context, workspace.WidgetFactory, movie, xmlViewModel, doNotUseGeneratedPrefabs: true, hotReloadEnabled: false);
        Assert.That(xml, Is.InstanceOf<GauntletMovie>(), "the XML path was not taken");
        var compiled = CompiledMovie.Build(workspace, movie, typeof(UsedListRootVM), ui);
        compiled.SetDataSource(compiledViewModel);

        Assert.That(Texts(xml.RootWidget), Is.EqualTo(expected), "XML");
        Assert.That(Texts(compiled.Root), Is.EqualTo(expected), "compiled");

        if (change is null)
            return;
        change(xmlViewModel);
        change(compiledViewModel);
        Assert.That(Texts(xml.RootWidget), Is.EqualTo(expectedAfter), "XML after the change");
        Assert.That(Texts(compiled.Root), Is.EqualTo(expectedAfter), "compiled after the change");
    }

    [Test]
    public void AListAtTheUsedPrefabsRoot_BuildsItsItems() => AssertSameTexts("UsedListPlainMovie",
    [
        ("UsedListPlainMovie", """<Prefab><Window><Widget><Children><UsedListPlainPart /></Children></Widget></Window></Prefab>"""),
        ("UsedListPlainPart", ListPart),
    ], ["root0", "root1"]);

    [Test]
    public void AListAtTheUsedPrefabsRoot_BelowAScope_BuildsItsItems() => AssertSameTexts("UsedListBelowMovie",
    [
        ("UsedListBelowMovie", """<Prefab><Window><Widget><Children><Widget DataSource="{Child}"><Children><UsedListBelowPart /></Children></Widget></Children></Widget></Window></Prefab>"""),
        ("UsedListBelowPart", ListPart),
    ], ["child0"]);

    /// <summary>
    /// Verifies item construction when the consuming widget declares the data source while item templates reside on the sub-prefab root.
    /// </summary>
    [Test]
    public void AListDataSourceOnTheUsingWidget_AndItemTemplatesOnTheRoot_BuildsItsItems() => AssertSameTexts("UsedListDeclaredMovie",
    [
        ("UsedListDeclaredMovie", """<Prefab><Window><Widget><Children><UsedListDeclaredPart DataSource="{Items}" /></Children></Widget></Window></Prefab>"""),
        ("UsedListDeclaredPart", """<Prefab><Window><ListPanel><ItemTemplate><LabelWidget Label="@Label" /></ItemTemplate></ListPanel></Window></Prefab>"""),
    ], ["root0", "root1"]);

    /// <summary>
    /// Verifies that consuming widgets with distinct property bindings correctly construct sub-prefab list items.
    /// </summary>
    [Test]
    public void AUsingWidgetThatBindsToo_BuildsTheItems() => AssertSameTexts("UsedListBindsMovie",
    [
        ("UsedListBindsMovie", """<Prefab><Window><Widget><Children><UsedListBindsPart IsVisible="@Shown" /></Children></Widget></Window></Prefab>"""),
        ("UsedListBindsPart", ListPart),
    ], ["root0", "root1"]);

    /// <summary>
    /// Verifies that post-initialization collection insertions construct corresponding item widgets in the consuming prefab tree.
    /// </summary>
    [Test]
    public void AnItemAddedAfterOpening_IsBuilt() => AssertSameTexts("UsedListChangedMovie",
    [
        ("UsedListChangedMovie", """<Prefab><Window><Widget><Children><UsedListChangedPart /></Children></Widget></Window></Prefab>"""),
        ("UsedListChangedPart", ListPart),
    ], ["root0", "root1"], viewModel => viewModel.Items.Add(new UsedListItemVM("root2")), ["root0", "root1", "root2"]);

    /// <summary>
    /// Verifies that traversing past the movie root via parent scope navigators (<c>..\..\..</c>) yields empty collection bindings,
    /// matching XML loader path resolution.
    /// </summary>
    [Test]
    public void AListBelowTheEmptyPathAboveTheMovieRoot_HasNoItems() => AssertSameTexts("UsedListEmptyRootMovie",
    [
        ("UsedListEmptyRootMovie", """<Prefab><Window><Widget DataSource="{A\B}"><Children><UsedListEmptyRootOuter /></Children></Widget></Window></Prefab>"""),
        ("UsedListEmptyRootOuter", """<Prefab><Window><ListPanel DataSource="{Items}"><ItemTemplate><Widget><Children><Widget DataSource="{..\..\..}"><Children><UsedListEmptyRootInner /></Children></Widget></Children></Widget></ItemTemplate></ListPanel></Window></Prefab>"""),
        ("UsedListEmptyRootInner", ListPart),
    ], []);

    [Test]
    public void AListBelowTheEmptyPath_InTheMovieItself_HasNoItems() => AssertSameTexts("UsedListEmptyFlatMovie",
    [
        ("UsedListEmptyFlatMovie", """<Prefab><Window><Widget><Children><ListPanel DataSource="{Items}"><ItemTemplate><Widget><Children><Widget DataSource="{..\..\..}"><Children><ListPanel DataSource="{Items}"><ItemTemplate><LabelWidget Label="@Label" /></ItemTemplate></ListPanel></Children></Widget></Children></Widget></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>"""),
    ], []);

    [Test]
    public void AListBelowTheEmptyPath_ThroughOneUsedPrefab_HasNoItems() => AssertSameTexts("UsedListEmptyOneMovie",
    [
        ("UsedListEmptyOneMovie", """<Prefab><Window><Widget><Children><ListPanel DataSource="{Items}"><ItemTemplate><Widget><Children><Widget DataSource="{..\..\..}"><Children><UsedListEmptyOneInner /></Children></Widget></Children></Widget></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>"""),
        ("UsedListEmptyOneInner", ListPart),
    ], []);
}

public class UsedListItemVM : ViewModel
{
    public UsedListItemVM(string label) => Label = label;
    public string Label { get; }
}

public class UsedListChildVM : ViewModel
{
    public MBBindingList<UsedListItemVM> Items { get; } = [new("child0")];
}

public class UsedListBVM : ViewModel
{
    public MBBindingList<UsedListItemVM> Items { get; } = [new("b0"), new("b1")];
}

public class UsedListAVM : ViewModel
{
    public UsedListBVM B { get; } = new();
    public MBBindingList<UsedListItemVM> Items { get; } = [new("a0")];
}

public class UsedListRootVM : ViewModel
{
    public UsedListAVM A { get; } = new();
    public UsedListChildVM Child { get; } = new();
    public bool Shown => true;
    public MBBindingList<UsedListItemVM> Items { get; } = [new("root0"), new("root1")];
}
