using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.Tests.Oracle;

using NUnit.Framework;

using System;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.Oracle;

/// <summary>
/// Tests code generation and oracle parity for official game prefabs previously declined by the code generator:
/// parent scope paths above generated class roots (<c>{..}</c>), undefined widget tags falling back to base <see cref="Widget"/>,
/// and indexed list paths (<c>{List\0\Prop}</c>).
/// </summary>
[NonParallelizable]
public class DeclinedPrefabTests
{
    private static OracleResult Bind(params (string Name, string Xml)[] prefabs)
    {
        using var workspace = new PrefabWorkspace(prefabs);
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(DeclinedPrefabTests));
        return new BindingLoaderOracle(workspace.WidgetFactory, ui.SpriteData, ui.BrushFactory, ui.Context, [typeof(LabelWidget).Assembly])
            .Run([prefabs[0].Name]).Single();
    }

    private static OracleResult Build(params (string Name, string Xml)[] prefabs)
    {
        using var workspace = new PrefabWorkspace(prefabs);
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(DeclinedPrefabTests));
        var plainFactory = new TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory(workspace.ResourceDepot, "Prefabs");
        plainFactory.Initialize();
        return new LoaderOracle(plainFactory, ui.SpriteData, ui.BrushFactory, ui.Context, [typeof(LabelWidget).Assembly])
            .Run([prefabs[0].Name]).Single();
    }

    private static void AssertMatches(OracleResult result) =>
        Assert.That(result.Outcome, Is.EqualTo(OracleOutcome.Match), () => $"{result.Detail}{Environment.NewLine}{string.Join(Environment.NewLine, result.Differences.Take(10))}");

    // --- Parent scope navigation above root --------------------------------------------------------------------------

    /// <summary>
    /// Verifies parent scope navigation when an instantiation widget binds attributes alongside a child prefab using <c>{..}</c>.
    /// </summary>
    [Test]
    public void APrefabClimbingAboveItsRoot_WhoseInstanceBindsItsOwn() => AssertMatches(Bind(
        ("AboveRootMovie", @"<Prefab><Window><Widget><Children><AboveRootCard DataSource=""{Selected}"" IsVisible=""@IsShown"" /></Children></Widget></Window></Prefab>"),
        ("AboveRootCard", @"<Prefab><Window><Widget><Children><LabelWidget Label=""@Name"" /><LabelWidget DataSource=""{..}"" Label=""@Title"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies two-level parent scope navigation climbing out of a nested prefab and descending into sibling properties (<c>{..\..\Header}</c>).
    /// </summary>
    [Test]
    public void APrefabClimbingTwoLevelsAndDownAgain() => AssertMatches(Bind(
        ("TwoUpMovie", @"<Prefab><Window><Widget DataSource=""{Friends}""><Children><TwoUpTab DataSource=""{ActiveService}"" IsVisible=""@IsShown"" /></Children></Widget></Window></Prefab>"),
        ("TwoUpTab", @"<Prefab><Window><Widget><Children><LabelWidget DataSource=""{..\..\Header}"" Label=""@Text"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies parent scope navigation from within a list item template climbing to the list container's parent scope (<c>{..\..}</c>).
    /// </summary>
    [Test]
    public void AListItemClimbingToTheOwnerOfItsList() => AssertMatches(Bind(
        ("ItemUpMovie", @"<Prefab><Window><ListPanel DataSource=""{Items}""><ItemTemplate><Widget><Children><LabelWidget Label=""@Name"" /><LabelWidget DataSource=""{..\..}"" Label=""@Title"" /></Children></Widget></ItemTemplate></ListPanel></Window></Prefab>")));

    /// <summary>
    /// Verifies multi-level parent scope navigation from a sub-prefab inside an item template climbing out to the collection scope (<c>{..\..\..}</c>).
    /// </summary>
    [Test]
    public void APrefabInAListItemClimbingOutOfBoth() => AssertMatches(Bind(
        ("NestedUpMovie", @"<Prefab><Window><ListPanel DataSource=""{Items}""><ItemTemplate><NestedUpHero DataSource=""{Captain}"" IsVisible=""@IsShown"" /></ItemTemplate></ListPanel></Window></Prefab>"),
        ("NestedUpHero", @"<Prefab><Window><Widget><Children><LabelWidget Label=""@Name"" /><LabelWidget DataSource=""{..\..\..}"" Label=""@Title"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies command bindings targeting parent scopes above the sub-prefab root (<c>..\ExecuteClose</c>).
    /// </summary>
    [Test]
    public void ACommandAboveThePrefabsRoot() => AssertMatches(Bind(
        ("CommandUpMovie", @"<Prefab><Window><Widget><Children><CommandUpCard DataSource=""{Selected}"" IsVisible=""@IsShown"" /></Children></Widget></Window></Prefab>"),
        ("CommandUpCard", @"<Prefab><Window><ButtonWidget DoNotPassEventsToChildren=""true"" Command.Click=""..\ExecuteClose""><Children><LabelWidget Label=""@Name"" /></Children></ButtonWidget></Window></Prefab>")));

    // --- Undefined widget tag fallback -------------------------------------------------------------------------------

    /// <summary>
    /// Verifies that unrecognized widget XML tags instantiate fallback base <see cref="Widget"/> instances, matching XML loader parity.
    /// </summary>
    [Test]
    public void AnUnknownWidgetName_IsAPlainWidget() => AssertMatches(Build(
        ("UnknownNameMovie", @"<Prefab><Window><Widget><Children><NoSuchWidgetClass MarginTop=""3"" /></Children></Widget></Window></Prefab>")));

    [Test]
    public void AnUnknownWidgetName_AsAListsItemTemplate() => AssertMatches(Bind(
        ("UnknownItemMovie", @"<Prefab><Window><ListPanel DataSource=""{Incomes}""><ItemTemplate><NoSuchWidgetClass /></ItemTemplate></ListPanel></Window></Prefab>")));

    [Test]
    public void AnUnknownWidgetName_IsReported()
    {
        using var workspace = new PrefabWorkspace(
            ("ReportedUnknownMovie", @"<Prefab><Window><Widget><Children><NoSuchWidgetClass /><NoSuchWidgetClass /></Children></Widget></Window></Prefab>"));
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(workspace.WidgetFactory);
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(DeclinedPrefabTests));
        var generator = new PrefabCodeGenerator("Bannerlord.UIExtenderEx.AutoGenerated.ReportedUnknown", workspace.WidgetFactory, ui.SpriteData, ui.BrushFactory);
        generator.AddMovie("ReportedUnknownMovie", "Variant", typeof(TaleWorlds.Library.ViewModel));

        generator.GenerateInMemory();

        Assert.That(generator.Warnings, Is.EqualTo(new[]
        {
            "'NoSuchWidgetClass' in prefab 'ReportedUnknownMovie' is neither a widget class nor a prefab; it is built as a plain Widget, as the XML loader builds it.",
        }), "once per name, however often it is used");
    }

    // --- Indexed list path resolution --------------------------------------------------------------------------------

    /// <summary>
    /// Verifies data source path binding into collection items by explicit numerical index (e.g. <c>{Perks\0\CandidatePerks}</c>).
    /// </summary>
    [Test]
    public void APathIntoAListByIndex() => AssertMatches(Bind(
        ("IndexedMovie", @"<Prefab><Window><Widget><Children><ListPanel DataSource=""{Perks\0\CandidatePerks}""><ItemTemplate><LabelWidget Label=""@Name"" /></ItemTemplate></ListPanel><LabelWidget DataSource=""{Perks\1}"" Label=""@Title"" /></Children></Widget></Window></Prefab>")));
}
