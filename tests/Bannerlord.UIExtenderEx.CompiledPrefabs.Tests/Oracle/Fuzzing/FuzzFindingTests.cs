using Bannerlord.UIExtenderEx.Tests.Oracle;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.Oracle.Fuzzing;

/// <summary>
/// Contains minimal reproduction test cases discovered by <see cref="PrefabFuzzTests"/>, verified against loader oracles
/// to prevent regressions. In each test case, the first tuple represents the root movie prefab.
/// </summary>
[NonParallelizable]
public class FuzzFindingTests
{
    private static OracleResult Bind(params (string Name, string Xml)[] prefabs)
    {
        using var workspace = new PrefabWorkspace(prefabs);
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(FuzzFindingTests));
        return new BindingLoaderOracle(workspace.WidgetFactory, ui.SpriteData, ui.BrushFactory, ui.Context, [typeof(LabelWidget).Assembly])
            .Run([prefabs[0].Name]).Single();
    }

    private static OracleResult Build(params (string Name, string Xml)[] prefabs)
    {
        using var workspace = new PrefabWorkspace(prefabs);
        using var ui = new TestUIContext(workspace.ResourceDepot, nameof(FuzzFindingTests));
        // Instantiates a plain WidgetFactory without databinding extensions, matching static oracle builds.
        var plainFactory = new TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory(workspace.ResourceDepot, "Prefabs");
        plainFactory.Initialize();
        return new LoaderOracle(plainFactory, ui.SpriteData, ui.BrushFactory, ui.Context, [typeof(LabelWidget).Assembly])
            .Run([prefabs[0].Name]).Single();
    }

    private static void AssertMatches(OracleResult result) =>
        Assert.That(result.Outcome, Is.EqualTo(OracleOutcome.Match), () => $"{result.Detail}{Environment.NewLine}{string.Join(Environment.NewLine, result.Differences.Take(10))}");

    // --- Used prefabs whose root widget is a list with item templates -----------------------------------------------

    /// <summary>
    /// Verifies that a used list prefab constructs child items using root item templates.
    /// The instantiating widget and the prefab root constitute a single view to the loader,
    /// inheriting the root item templates. <see cref="UsedListPrefabTests"/> contains analogous
    /// structures with known view model types.
    /// </summary>
    [Test]
    public void AUsedListPrefab_BuildsItsItems() => AssertMatches(Bind(
        ("UsedListControlMovie", @"<Prefab><Window><Widget><Children><UsedListControlP /></Children></Widget></Window></Prefab>"),
        ("UsedListControlP", @"<Prefab><Window><ListPanel DataSource=""{Items}""><ItemTemplate><LabelWidget Label=""@Label"" /></ItemTemplate></ListPanel></Window></Prefab>")));

    [Test]
    public void AUsedListPrefab_BelowAScope_BuildsItsItems() => AssertMatches(Bind(
        ("UsedListBelowMovie", @"<Prefab><Window><Widget><Children><Widget DataSource=""{Child}""><Children><UsedListBelowP /></Children></Widget></Children></Widget></Window></Prefab>"),
        ("UsedListBelowP", @"<Prefab><Window><ListPanel DataSource=""{Items}""><ItemTemplate><LabelWidget Label=""@Label"" /></ItemTemplate></ListPanel></Window></Prefab>")));

    /// <summary>
    /// Verifies that a used list prefab whose instantiating widget declares a <c>DataSource</c> constructs child items.
    /// </summary>
    [Test]
    public void AUsedListPrefab_WithTheDataSourceOnTheUsingWidget_BuildsItsItems() => AssertMatches(Bind(
        ("UsedListDeclaredMovie", @"<Prefab><Window><Widget><Children><UsedListDeclaredP DataSource=""{Items}"" /></Children></Widget></Window></Prefab>"),
        ("UsedListDeclaredP", @"<Prefab><Window><ListPanel><ItemTemplate><LabelWidget Label=""@Label"" /></ItemTemplate></ListPanel></Window></Prefab>")));

    [Test]
    public void AUsedListPrefab_WhoseUsingWidgetBindsToo_BuildsItsItems() => AssertMatches(Bind(
        ("UsedListBindsMovie", @"<Prefab><Window><Widget><Children><UsedListBindsP IsVisible=""@Shown"" /></Children></Widget></Window></Prefab>"),
        ("UsedListBindsP", @"<Prefab><Window><ListPanel DataSource=""{Items}""><ItemTemplate><LabelWidget Label=""@Label"" /></ItemTemplate></ListPanel></Window></Prefab>")));

    [Test]
    public void AUsedListPrefab_WhoseItemsClimbToTheRoot() => AssertMatches(Bind(
        ("UsedListRootMovie", @"<Prefab><Window><Widget><Children><UsedListRootP /></Children></Widget></Window></Prefab>"),
        ("UsedListRootP", @"<Prefab><Window><ListPanel DataSource=""{Items}""><ItemTemplate><Widget DataSource=""{..\..}""><Children><LabelWidget Label=""@Title"" /></Children></Widget></ItemTemplate></ListPanel></Window></Prefab>")));

    // --- Empty paths one level above the movie root -----------------------------------------------------------------

    /// <summary>
    /// Verifies that a list positioned below an empty path navigation resolves correctly across two nested prefab instances.
    /// Navigation paths such as <c>Root\Items\0\..\..\..</c> simplify to an empty path, causing subsequent resolution
    /// to anchor at the root view model.
    /// </summary>
    [Test]
    public void AListBelowTheEmptyPath_ThroughTwoUsedPrefabs() => AssertMatches(Bind(
        ("EmptyRootMovie", @"<Prefab><Window><Widget DataSource=""{A\B}""><Children><EmptyRootP /></Children></Widget></Window></Prefab>"),
        ("EmptyRootP", @"<Prefab><Window><ListPanel DataSource=""{Items}""><ItemTemplate><Widget><Children><Widget DataSource=""{..\..\..}""><Children><EmptyRootQ /></Children></Widget></Children></Widget></ItemTemplate></ListPanel></Window></Prefab>"),
        ("EmptyRootQ", @"<Prefab><Window><ListPanel DataSource=""{Items}""><ItemTemplate><LabelWidget Label=""@Label"" /></ItemTemplate></ListPanel></Window></Prefab>")));

    [Test]
    public void AListBelowTheEmptyPath_ThroughOneUsedPrefab() => AssertMatches(Bind(
        ("EmptyRootOneMovie", @"<Prefab><Window><Widget><Children><ListPanel DataSource=""{Items}""><ItemTemplate><Widget><Children><Widget DataSource=""{..\..\..}""><Children><EmptyRootOneQ /></Children></Widget></Children></Widget></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>"),
        ("EmptyRootOneQ", @"<Prefab><Window><ListPanel DataSource=""{Items}""><ItemTemplate><LabelWidget Label=""@Label"" /></ItemTemplate></ListPanel></Window></Prefab>")));

    [Test]
    public void AListBelowTheEmptyPath_InTheMovieItself() => AssertMatches(Bind(
        ("EmptyRootFlatMovie", @"<Prefab><Window><Widget><Children><ListPanel DataSource=""{Items}""><ItemTemplate><Widget><Children><Widget DataSource=""{..\..\..}""><Children><ListPanel DataSource=""{Items}""><ItemTemplate><LabelWidget Label=""@Label"" /></ItemTemplate></ListPanel></Children></Widget></Children></Widget></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that an <c>Id</c> attribute bound to a view model property resolves correctly.
    /// </summary>
    [Test]
    public void ABoundId() => AssertMatches(Bind(
        ("BoundIdMovie", @"<Prefab><Window><Widget DataSource=""{Child}""><Children><Widget Id=""@Title"" /></Children></Widget></Window></Prefab>")));

    [Test]
    public void ABoundId_OnAPrefabInstance() => AssertMatches(Bind(
        ("BoundIdInstanceMovie", @"<Prefab><Window><Widget DataSource=""{Child}""><Children><BoundIdPart Id=""@Title"" /></Children></Widget></Window></Prefab>"),
        ("BoundIdPart", @"<Prefab><Window><Widget /></Window></Prefab>")));

    /// <summary>
    /// Verifies that a bound <c>Id</c> attribute on a prefab instance whose root declares a <c>DataSource</c>
    /// resolves within the root scope rather than the surrounding container scope.
    /// </summary>
    [Test]
    public void ABoundId_OnAPrefabInstanceWhoseRootDeclaresADataSource() => AssertMatches(Bind(
        ("BoundIdScopedMovie", @"<Prefab><Window><Widget DataSource=""{Child}""><Children><BoundIdScopedPart Id=""@Title"" /></Children></Widget></Window></Prefab>"),
        ("BoundIdScopedPart", @"<Prefab><Window><Widget DataSource=""{Other}"" /></Window></Prefab>")));

    [Test]
    public void ACommand_OnAPrefabInstanceWhoseRootDeclaresADataSource() => AssertMatches(Bind(
        ("ScopedInstanceMovie", @"<Prefab><Window><Widget><Children><ScopedInstancePart Command.Click=""ExecuteGo"" /></Children></Widget></Window></Prefab>"),
        ("ScopedInstancePart", @"<Prefab><Window><ButtonWidget DataSource=""{Other}"" DoNotPassEventsToChildren=""true""><Children><LabelWidget /></Children></ButtonWidget></Window></Prefab>")));

    [Test]
    public void AChild_OfAPrefabInstanceWhoseRootDeclaresADataSource() => AssertMatches(Bind(
        ("ScopedChildMovie", @"<Prefab><Window><Widget><Children><ScopedChildPart><Children><LabelWidget Label=""@Label"" /></Children></ScopedChildPart></Children></Widget></Window></Prefab>"),
        ("ScopedChildPart", @"<Prefab><Window><Widget DataSource=""{Other}"" /></Window></Prefab>")));

    [Test]
    public void AnInstanceDataSource_StillReplacesTheRootsOwn() => AssertMatches(Bind(
        ("ReplacedScopeMovie", @"<Prefab><Window><Widget><Children><ReplacedScopePart DataSource=""{Child}"" Label=""@Label"" /></Children></Widget></Window></Prefab>"),
        ("ReplacedScopePart", @"<Prefab><Window><LabelWidget DataSource=""{Other}"" /></Window></Prefab>")));

    [Test]
    public void ARootDataSource_TakenFromAParameterTheInstancePasses() => AssertMatches(Bind(
        ("PassedScopeMovie", @"<Prefab><Window><Widget><Children><PassedScopePart Parameter.Source=""{Child}"" Label=""@Label"" /><PassedScopePart Label=""@Label"" /></Children></Widget></Window></Prefab>"),
        ("PassedScopePart", @"<Prefab><Parameters><Parameter Name=""Source"" DefaultValue=""Other"" /></Parameters><Window><LabelWidget DataSource=""*Source"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies static evaluation behavior when passed children reside below a prefab widget that fails attribute evaluation
    /// due to a missing constant.
    /// </summary>
    [Test]
    public void PassedChildren_BelowAUsedPrefabWidgetThatGivesUpOnItsAttributes() => AssertMatches(Build(
        ("AbandonedRootMovie", @"<Prefab><Window><Widget><Children><AbandonedRootPart><Children><LabelWidget Label=""passed"" /></Children></AbandonedRootPart></Children></Widget></Window></Prefab>"),
        ("AbandonedRootPart", @"<Prefab><Window><Widget MaxHeight=""!Missing""><Children><Widget><LogicalChildrenLocation /></Widget></Children></Widget></Window></Prefab>")));

    [Test]
    public void PassedChildren_OfALogicalChildrenLocationThatGivesUpOnItsAttributes() => AssertMatches(Build(
        ("AbandonedLocationMovie", @"<Prefab><Window><Widget><Children><AbandonedLocationPart><Children><LabelWidget Label=""passed"" /></Children></AbandonedLocationPart></Children></Widget></Window></Prefab>"),
        ("AbandonedLocationPart", @"<Prefab><Window><Widget><Children><Widget><Children><Widget MaxHeight=""!Missing""><LogicalChildrenLocation /></Widget></Children></Widget></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that a <c>&lt;LogicalChildrenLocation /&gt;</c> declared on a component root is bypassed by
    /// <c>GetLogicalOrDefaultChildrenLocation</c>, preserving passed child attribute evaluation.
    /// </summary>
    [Test]
    public void PassedChildren_OfALocationOnThePartsRoot() => AssertMatches(Build(
        ("RootLocationMovie", @"<Prefab><Window><ButtonWidget><Children><RootLocationPart><Children><ButtonWidget VisualDefinition=""Tab_V1"" /></Children></RootLocationPart></Children></ButtonWidget></Window></Prefab>"),
        ("RootLocationPart", @"<Prefab><Window><Widget MaxHeight=""!Missing""><LogicalChildrenLocation /></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that an unreferenced visual definition with an uncomputable constant does not fail movie generation.
    /// </summary>
    [Test]
    public void AVisualStateValueThatCannotBeComputed_InADefinitionNobodyTakes() => AssertMatches(Build(
        ("UnusedFailingStateMovie", @"<Prefab><Constants><Constant Name=""C1"" Value=""2.5"" Additive=""!Missing"" /></Constants><VisualDefinitions><VisualDefinition Name=""Tab.V1""><VisualState State=""Pressed"" GotMarginTop=""!C1"" PositionYOffset=""!C1"" /></VisualDefinition></VisualDefinitions><Window><ButtonWidget /></Window></Prefab>")));

    /// <summary>
    /// Verifies that referencing a visual definition with an uncomputable constant throws an exception during widget creation.
    /// </summary>
    [Test]
    public void AVisualStateValueThatCannotBeComputed_InADefinitionAWidgetTakes() => AssertMatches(Build(
        ("UsedFailingStateMovie", @"<Prefab><Constants><Constant Name=""C1"" Value=""2.5"" Additive=""!Missing"" /></Constants><VisualDefinitions><VisualDefinition Name=""Tab.V1""><VisualState State=""Pressed"" PositionYOffset=""!C1"" /></VisualDefinition></VisualDefinitions><Window><ButtonWidget VisualDefinition=""Tab.V1"" MarginLeft=""3"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies that view model properties and commands matching C# language keywords bind properly using verbatim identifiers (e.g. <c>@true</c>).
    /// </summary>
    [Test]
    public void MembersNamedAsKeywords() => AssertMatches(Bind(
        ("KeywordMovie", @"<Prefab><Window><Widget DataSource=""{true}""><Children><ButtonWidget DoNotPassEventsToChildren=""true"" Command.Click=""while""><Children><LabelWidget Label=""@false"" /></Children></ButtonWidget></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that children passed into a logical children location bind within the context scope of that location.
    /// </summary>
    [Test]
    public void PassedChildren_BindInTheScopeOfTheLocation() => AssertMatches(Bind(
        ("PassedScopeMovie", @"<Prefab><Window><ButtonWidget><Children><PassedScopePart><Children><BrushWidget Command.Click=""Child\ExecuteA"" /><LabelWidget Label=""@Title"" /></Children></PassedScopePart></Children></ButtonWidget></Window></Prefab>"),
        ("PassedScopePart", @"<Prefab><Window><Widget><Children><Widget DataSource=""{Rows\1}""><LogicalChildrenLocation /></Widget></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that passed children bind within the logical children location scope across child view model scopes and parent navigations.
    /// </summary>
    [Test]
    public void PassedChildren_BindInTheScopeOfTheLocation_ThroughAChild() => AssertMatches(Bind(
        ("PassedChildScopeMovie", @"<Prefab><Window><Widget><Children><PassedChildScopePart><Children><LabelWidget Label=""@Title"" /></Children></PassedChildScopePart><PassedClimbScopePart><Children><LabelWidget Label=""@Label"" /></Children></PassedClimbScopePart></Children></Widget></Window></Prefab>"),
        ("PassedChildScopePart", @"<Prefab><Window><Widget><Children><Widget DataSource=""{Child}""><Children><Widget><LogicalChildrenLocation /></Widget></Children></Widget></Children></Widget></Window></Prefab>"),
        ("PassedClimbScopePart", @"<Prefab><Window><Widget DataSource=""{Other}""><Children><Widget DataSource=""{..\Child}""><LogicalChildrenLocation /></Widget></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that a widget whose immediate data source resolves to null still executes commands targeting an ancestor scope.
    /// </summary>
    [Test]
    public void ACommand_OfAWidgetWhoseScopeIsNull() => AssertMatches(Bind(
        ("NullScopeCommandMovie", @"<Prefab><Window><ButtonWidget><Children><Widget DataSource=""{Child}""><Children><ButtonWidget DataSource=""{Missing\Deeper}"" Command.Click=""..\..\ExecuteB"" /></Children></Widget><BrushWidget Command.Click=""ExecuteB"" /></Children></ButtonWidget></Window></Prefab>")));

    /// <summary>
    /// Verifies that events fired during initial data binding synchronization suppress command execution, matching GauntletView behavior.
    /// </summary>
    [Test]
    public void AnEventFiredWhileTheValuesAreWritten_RunsNoCommand() => AssertMatches(Bind(
        ("OpenedOnSetMovie", @"<Prefab><Window><Widget><Children><OpenedOnSetWidget State=""@Title"" Command.Opened=""ExecuteA"" /><Widget DataSource=""{Child}""><Children><OpenedOnSetWidget State=""@Name"" Command.Opened=""ExecuteB"" /></Children></Widget><ListPanel DataSource=""{Items}""><ItemTemplate><OpenedOnSetWidget State=""@Name"" Command.Opened=""ExecuteA"" /></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that internal widget event handlers execute prior to bound command handlers.
    /// </summary>
    [Test]
    public void AWidgetsOwnHandler_RunsBeforeTheCommand() => AssertMatches(Bind(
        ("SelfListeningMovie", @"<Prefab><Window><SelfListeningWidget ListensToItself=""true"" Clicks=""@Count"" Command.Click=""ExecuteA"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies command and property binding resolution when a prefab is instantiated with <c>{..}</c> at the movie root.
    /// </summary>
    [Test]
    public void APrefabUsedAboveTheMovieRoot() => AssertMatches(Bind(
        ("AboveMovieRootMovie", @"<Prefab><Window><ListPanel><Children><AboveMovieRootPart DataSource=""{..}"" /></Children></ListPanel></Window></Prefab>"),
        ("AboveMovieRootPart", @"<Prefab><Window><Widget><Children><ButtonWidget Command.Click=""Child\ExecuteA"" /><LabelWidget Label=""@Title"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that passed children route to the shallower of two logical children locations in the hierarchy.
    /// </summary>
    [Test]
    public void PassedChildren_GoToTheShallowerOfTwoLocations() => AssertMatches(Build(
        ("TwoLocationsMovie", @"<Prefab><Window><Widget><Children><TwoLocationsPart><Children><LabelWidget Label=""passed"" /></Children></TwoLocationsPart></Children></Widget></Window></Prefab>"),
        ("TwoLocationsPart", @"<Prefab><Window><Widget><Children><Widget Id=""Deep""><Children><Widget><LogicalChildrenLocation /></Widget></Children></Widget><Widget Id=""Shallow""><LogicalChildrenLocation /></Widget></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that children passed to a prefab lacking a logical children location remain direct children of the instance widget.
    /// </summary>
    [Test]
    public void PassedChildren_WithoutALogicalChildrenLocation() => AssertMatches(Build(
        ("DefaultLocationMovie", @"<Prefab><Window><Widget><Children><DefaultLocationPart><Children><LabelWidget Label=""passed"" /></Children></DefaultLocationPart></Children></Widget></Window></Prefab>"),
        ("DefaultLocationPart", @"<Prefab><Window><Widget MaxHeight=""!Missing""><Children><Widget /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies parameter resolution for passed children routed through a logical children location.
    /// Confirms the intentional divergence where compiled prefabs resolve parameters in their declaring prefab scope
    /// rather than falling back to default values due to intermediate template nesting.
    /// </summary>
    [Test]
    public void APassedChildsParameter_InALogicalChildrenLocation_IsResolvedWhereItIsDeclared()
    {
        var result = Build(
            ("LostParameterMovie", @"<Prefab><Window><LostParameterOuter Parameter.X=""given"" /></Window></Prefab>"),
            ("LostParameterOuter", @"<Prefab><Parameters><Parameter Name=""X"" DefaultValue=""default"" /></Parameters><Window><LostParameterInner><Children><LabelWidget Label=""*X"" /></Children></LostParameterInner></Window></Prefab>"),
            ("LostParameterInner", @"<Prefab><Window><Widget><Children><Widget><LogicalChildrenLocation /></Widget></Children></Widget></Window></Prefab>"));

        Assert.That(result.Outcome, Is.EqualTo(OracleOutcome.Differs), () => result.Detail);
        Assert.That(result.Differences, Is.EqualTo(new[] { "root[0][0].Label: XML 'default', compiled 'given'" }));
        Assert.That(result.XmlAsserts, Is.EqualTo(result.CompiledAsserts));
    }

    /// <summary>
    /// Verifies that explicitly forwarding parameters through nested prefabs preserves parameter values in both XML and compiled pipelines.
    /// </summary>
    [Test]
    public void APassedChildsParameter_InALogicalChildrenLocation_PassedOn() => AssertMatches(Build(
        ("PassedOnParameterMovie", @"<Prefab><Window><PassedOnParameterOuter Parameter.X=""given"" /></Window></Prefab>"),
        ("PassedOnParameterOuter", @"<Prefab><Parameters><Parameter Name=""X"" DefaultValue=""default"" /></Parameters><Window><PassedOnParameterInner Parameter.X=""*X""><Children><LabelWidget Label=""*X"" /></Children></PassedOnParameterInner></Window></Prefab>"),
        ("PassedOnParameterInner", @"<Prefab><Window><Widget><Children><Widget><LogicalChildrenLocation /></Widget></Children></Widget></Window></Prefab>")));

    [Test]
    public void APassedChildsParameter_WithoutALogicalChildrenLocation() => AssertMatches(Build(
        ("OwnParameterMovie", @"<Prefab><Window><Widget><Children><OwnParameterOuter Parameter.X=""fromMovie"" /></Children></Widget></Window></Prefab>"),
        ("OwnParameterOuter", @"<Prefab><Parameters><Parameter Name=""X"" DefaultValue=""outerDefault"" /></Parameters><Window><Widget><Children><OwnParameterInner><Children><LabelWidget Label=""*X"" /></Children></OwnParameterInner></Children></Widget></Window></Prefab>"),
        ("OwnParameterInner", @"<Prefab><Window><Widget><Children><Widget /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies two-way binding write-back behavior when an alignment property binds to an enumeration view model property.
    /// </summary>
    [Test]
    public void AnAlignmentBoundToAnEnumProperty() => AssertMatches(Bind(
        ("AlignmentWriteBackMovie", @"<Prefab><Window><BrushWidget VerticalAlignment=""@Ratio"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies write-back type coercion when numeric properties of mismatched types bind to a shared view model property.
    /// </summary>
    [Test]
    public void AFloatWrittenBackIntoAnIntProperty() => AssertMatches(Bind(
        ("NumberCastMovie", @"<Prefab><Window><Widget><Children><SelfListeningWidget Clicks=""@Count"" /><Widget SuggestedWidth=""@Count"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies parent scope data source parameter resolution adjacent to an item list sharing an identical property name.
    /// </summary>
    [Test]
    public void AParentScopeDataSourcePassedAsAParameter_NextToAListWithTheSameName() => AssertMatches(Bind(
        ("DropdownHintMovie", @"<Prefab><Window><Widget><Children><DropdownHintPart Parameter.SelectorDataSource=""{SelectionSelector}"" Parameter.HintDataSource=""{..\Hint}"" /></Children></Widget></Window></Prefab>"),
        ("DropdownHintPart", @"<Prefab><Parameters><Parameter Name=""SelectorDataSource"" DefaultValue=""SelectorDataSource"" /><Parameter Name=""HintDataSource"" DefaultValue="""" /></Parameters>
<Window><ListPanel DataSource=""*SelectorDataSource""><Children>
  <ButtonWidget DataSource=""*HintDataSource"" Command.Click=""ExecuteBeginHint"" />
  <ListPanel DataSource=""{ItemList}""><ItemTemplate><ButtonWidget><Children><ButtonWidget DataSource=""{Hint}"" Command.Click=""ExecuteBeginHint"" /></Children></ButtonWidget></ItemTemplate></ListPanel>
</Children></ListPanel></Window></Prefab>")));

    /// <summary>
    /// Verifies that commands declared on a prefab instance override commands of the same name declared on the prefab root.
    /// </summary>
    [Test]
    public void ACommandOnAPrefabInstance_ReplacesTheRootsOfTheSameName() => AssertMatches(Bind(
        ("ReplacedCommandMovie", @"<Prefab><Window><Widget><Children><ReplacedCommandPart Command.Click=""ExecuteInstance"" /></Children></Widget></Window></Prefab>"),
        ("ReplacedCommandPart", @"<Prefab><Window><ButtonWidget Command.Click=""ExecuteRoot"" Command.AlternateClick=""ExecuteRootAlternate"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies that instance command bindings override root command bindings through a chain of nested prefab instances.
    /// </summary>
    [Test]
    public void ACommandOnAPrefabInstance_ReplacesThroughAChainOfRoots() => AssertMatches(Bind(
        ("ReplacedChainMovie", @"<Prefab><Window><Widget><Children><ReplacedChainOuter Command.Click=""ExecuteInstance"" /></Children></Widget></Window></Prefab>"),
        ("ReplacedChainOuter", @"<Prefab><Window><ReplacedChainInner Command.AlternateClick=""ExecuteOuter"" /></Window></Prefab>"),
        ("ReplacedChainInner", @"<Prefab><Window><ButtonWidget Command.Click=""ExecuteInner"" Command.AlternateClick=""ExecuteInnerAlternate"" Command.HoverBegin=""ExecuteInnerHover"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies that two-way write-back writes the event-announced value rather than re-reading the widget property directly.
    /// </summary>
    [Test]
    public void AWriteBack_TakesTheAnnouncedValue() => AssertMatches(Bind(
        ("AnnouncedValueMovie", @"<Prefab><Window><ReenteringValueWidget Value=""@Ratio"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies that write-back writes null back into target view model properties when a bound resource is missing.
    /// </summary>
    [Test]
    public void ANullAnnounced_IsWrittenBackIntoAPropertyOfAnotherType() => AssertMatches(Bind(
        ("NullWriteBackMovie", @"<Prefab><Window><BrushWidget Brush=""@ImageID"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies parent scope navigation (<c>{..}</c>) inside a prefab whose instantiating widget specifies a <c>DataSource</c>.
    /// </summary>
    [Test]
    public void AParentScopeInsideAPrefabWhoseInstanceDeclaresADataSource() => AssertMatches(Bind(
        ("ClimbOutMovie", @"<Prefab><Window><Widget><Children><ClimbOutPart DataSource=""{Friends}"" /></Children></Widget></Window></Prefab>"),
        ("ClimbOutPart", @"<Prefab><Window><Widget><Children><LabelWidget DataSource=""{..}"" Label=""@Title"" /><LabelWidget Label=""@Name"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies parent scope navigation across a chain of nested prefabs whose root instantiator specifies a <c>DataSource</c>.
    /// </summary>
    [Test]
    public void AParentScopeInsideAChainOfPrefabsWhoseInstanceDeclaresADataSource() => AssertMatches(Bind(
        ("ClimbOutChainMovie", @"<Prefab><Window><Widget><Children><ClimbOutChainOuter DataSource=""{Friends}"" /></Children></Widget></Window></Prefab>"),
        ("ClimbOutChainOuter", @"<Prefab><Window><ClimbOutChainInner /></Window></Prefab>"),
        ("ClimbOutChainInner", @"<Prefab><Window><Widget DataSource=""{Ignored}""><Children><LabelWidget DataSource=""{..}"" Label=""@Title"" /><LabelWidget Label=""@Name"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies data source resolution when a prefab instance specifies <c>DataSource="{..}"</c> to navigate out of its enclosing scope.
    /// </summary>
    [Test]
    public void AnInstanceDataSourceAboveTheScopeAroundIt() => AssertMatches(Bind(
        ("ClimbBackMovie", @"<Prefab><Window><Widget><Children><Widget DataSource=""{Child}""><Children><LabelWidget Label=""@Name"" /><ClimbBackPart DataSource=""{..}"" /></Children></Widget></Children></Widget></Window></Prefab>"),
        ("ClimbBackPart", @"<Prefab><Window><Widget><Children><LabelWidget Label=""@Title"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies parent scope navigation (<c>DataSource="{..}"</c>) on a prefab instance within a list item template.
    /// </summary>
    [Test]
    public void AnInstanceDataSourceAboveTheScopeAroundIt_InAListItem() => AssertMatches(Bind(
        ("ClimbBackItemMovie", @"<Prefab><Window><Widget><Children><ListPanel DataSource=""{Items}""><ItemTemplate><Widget><Children><Widget DataSource=""{Child}""><Children><LabelWidget Label=""@Name"" /><ClimbBackItemPart DataSource=""{..}"" /></Children></Widget></Children></Widget></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>"),
        ("ClimbBackItemPart", @"<Prefab><Window><Widget><Children><LabelWidget Label=""@Title"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies navigation into sibling scopes (<c>DataSource="{..\Other}"</c>) from within a nested prefab instance.
    /// </summary>
    [Test]
    public void AnInstanceDataSourceIntoASiblingOfTheScopeAroundIt() => AssertMatches(Bind(
        ("ClimbSiblingMovie", @"<Prefab><Window><Widget><Children><Widget DataSource=""{Child}""><Children><LabelWidget Label=""@Name"" /><ClimbSiblingPart DataSource=""{..\Other}"" /></Children></Widget></Children></Widget></Window></Prefab>"),
        ("ClimbSiblingPart", @"<Prefab><Window><Widget><Children><LabelWidget Label=""@Title"" /><LabelWidget DataSource=""{..}"" Label=""@Label"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies navigation paths climbing above the initial scope supplied to a prefab instance.
    /// </summary>
    [Test]
    public void APathAboveTheScopeAPrefabIsHanded() => AssertMatches(Bind(
        ("ClimbTooFarMovie", @"<Prefab><Window><Widget><Children><ClimbTooFarPart DataSource=""{Child}"" /></Children></Widget></Window></Prefab>"),
        ("ClimbTooFarPart", @"<Prefab><Window><Widget><Children><LabelWidget DataSource=""{..\..}"" Label=""@Title"" /></Children></Widget></Window></Prefab>")));

    [Test]
    public void APathAboveTheScopeAPrefabIsHanded_UnderAScopeOfItsOwn() => AssertMatches(Bind(
        ("DeclinedClimbMovie", @"<Prefab><Window><Widget DataSource=""{Child}""><Children><DeclinedClimbPart DataSource=""{Child}"" /></Children></Widget></Window></Prefab>"),
        ("DeclinedClimbPart", @"<Prefab><Window><Widget><Children><LabelWidget DataSource=""{..\..}"" Label=""@Title"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that declining generation for a prefab that XML successfully opens is flagged as an oracle defect.
    /// </summary>
    [Test]
    public void ADeclineOfAPrefabXmlOpens_IsADefect() =>
        Assert.That(new OracleResult("DeclinedMovie", OracleOutcome.NotGenerated, [], "declined", 0, 0).IsDefect, Is.True);

    /// <summary>
    /// Verifies resolution of deeply climbing parent navigation paths exceeding standard nesting thresholds.
    /// </summary>
    [Test]
    public void APathFarAboveTheScopeAPrefabIsHanded() => AssertMatches(Bind(
        ("ClimbFarMovie", @"<Prefab><Window><Widget><Children><ClimbFarPart DataSource=""{Child}"" IsVisible=""@IsShown"" /></Children></Widget></Window></Prefab>"),
        ("ClimbFarPart", $@"<Prefab><Window><Widget><Children><LabelWidget DataSource=""{{{Climb(66)}}}"" Label=""@Title"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies parent path navigation from within deeply nested view model hierarchy scopes.
    /// </summary>
    [Test]
    public void APathFarAboveTheScopeAPrefabIsHanded_ToADataSourceThatIsThere() => AssertMatches(Bind(
        ("ClimbFarDeepMovie", $@"<Prefab><Window><Widget><Children><Widget DataSource=""{{{string.Join("\\", Enumerable.Range(1, 70).Select(x => "L" + x))}}}""><Children><ClimbFarDeepPart DataSource=""{{Child}}"" IsVisible=""@IsShown"" /></Children></Widget></Children></Widget></Window></Prefab>"),
        ("ClimbFarDeepPart", $@"<Prefab><Window><Widget><Children><LabelWidget DataSource=""{{{Climb(66)}}}"" Label=""@Title"" /><ButtonWidget DataSource=""{{{Climb(66)}\Other}}"" Command.Click=""ExecuteA"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies recursive self-referential prefab instantiation within list item templates with upward path navigation.
    /// </summary>
    [Test]
    public void APrefabUsedInItsOwnListItems_EachInstanceHigherUp() => AssertMatches(Bind(
        ("ClimbingSelfMovie", @"<Prefab><Window><Widget><Children><ClimbingSelfPart DataSource=""{Child}"" /></Children></Widget></Window></Prefab>"),
        ("ClimbingSelfPart", @"<Prefab><Window><Widget><Children><LabelWidget Label=""@Name"" /><ListPanel DataSource=""{Items}""><ItemTemplate><Widget><Children><Widget DataSource=""{..\..\..}""><Children><ClimbingSelfPart /></Children></Widget></Children></Widget></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that prefabs recursively instantiated both deeper and higher in list templates are declined with descriptive errors.
    /// </summary>
    [Test]
    public void APrefabUsedInItsOwnListItems_EverDeeperAndEverHigher_IsDeclined()
    {
        using var workspace = new PrefabWorkspace(
            ("DeeperAndHigherMovie", @"<Prefab><Window><Widget DataSource=""{Child}""><Children><DeeperAndHigherPart /></Children></Widget></Window></Prefab>"),
            ("DeeperAndHigherPart", @"<Prefab><Window><Widget><Children><LabelWidget DataSource=""{..}"" Label=""@Title"" /><ListPanel DataSource=""{Items}""><ItemTemplate><Widget><Children><DeeperAndHigherPart /><Widget DataSource=""{..\..\..}""><Children><DeeperAndHigherPart /></Children></Widget></Children></Widget></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>"));

        Assert.That(() => workspace.Generate("DeeperAndHigherMovie", typeof(CodegenTestVM)),
            Throws.InvalidOperationException.With.Message.Contains("both ever deeper and ever higher"));
    }

    /// <summary>
    /// Verifies that declining a prefab which the XML loader cannot load does not register as a defect.
    /// </summary>
    [Test]
    public void APrefabUsedInItsOwnListItems_EverDeeperAndEverHigher_XmlThrowsToo()
    {
        var result = Bind(
            ("DeeperAndHigherXmlMovie", @"<Prefab><Window><Widget DataSource=""{Child}""><Children><DeeperAndHigherXmlPart /></Children></Widget></Window></Prefab>"),
            ("DeeperAndHigherXmlPart", @"<Prefab><Window><Widget><Children><LabelWidget DataSource=""{..}"" Label=""@Title"" /><ListPanel DataSource=""{Items}""><ItemTemplate><Widget><Children><DeeperAndHigherXmlPart /><Widget DataSource=""{..\..\..}""><Children><DeeperAndHigherXmlPart /></Children></Widget></Children></Widget></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>"));

        Assert.That(result.Outcome, Is.EqualTo(OracleOutcome.XmlThrows), () => result.Detail);
        Assert.That(result.IsDefect, Is.False);
    }

    private static string Climb(int levels) => string.Join("\\", Enumerable.Repeat("..", levels));

    /// <summary>
    /// Verifies that declining prefabs that also fail in the XML loader is not treated as a defect.
    /// </summary>
    [Test]
    public void ADeclineOfAPrefabXmlCannotOpen_IsNoDefect()
    {
        var result = Bind(
            ("DeclinedMissingConstantMovie", @"<Prefab><Window><Widget><Children><DeclinedMissingConstantPart /></Children></Widget></Window></Prefab>"),
            ("DeclinedMissingConstantPart", @"<Prefab><Window><Widget DataSource=""!Missing""><Children><LabelWidget Label=""@Title"" /></Children></Widget></Window></Prefab>"));

        Assert.That(result.Outcome, Is.EqualTo(OracleOutcome.XmlThrows), () => result.Detail);
        Assert.That(result.Detail, Does.Contain("declined as well"));
        Assert.That(result.IsDefect, Is.False);
    }

    /// <summary>
    /// Verifies that item prefab root widgets bind to the item context regardless of root <c>DataSource</c> declarations.
    /// </summary>
    [Test]
    public void AnItemRoot_BindsAtTheItemWhateverItsTemplateDeclares() => AssertMatches(Bind(
        ("ItemRootScopeMovie", @"<Prefab><Window><Widget><Children><ListPanel DataSource=""{Items}""><ItemTemplate><ItemRootScopePart Parameter.DataSource=""{Child}"" /></ItemTemplate></ListPanel><ListPanel DataSource=""{Others}""><ItemTemplate><LabelWidget DataSource=""{Child}"" Label=""@Title"" /></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>"),
        ("ItemRootScopePart", @"<Prefab><Parameters><Parameter Name=""DataSource"" DefaultValue="""" /></Parameters><Window><LabelWidget DataSource=""*DataSource"" Label=""@Title"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies child scope binding behavior below navigation paths that climb above the movie root.
    /// </summary>
    [Test]
    public void AChildScope_BelowAScopeAboveTheMoviesRoot() => AssertMatches(Bind(
        ("AboveRootMovie", @"<Prefab><Window><Widget><Children><Widget DataSource=""{..}""><Children><LabelWidget DataSource=""{Child}"" Label=""@Title"" /><LabelWidget Label=""@Name"" /></Children></Widget></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that replacing a data source does not inadvertently write back values to the superseded view model.
    /// </summary>
    [Test]
    public void AnInstanceAndItsRoot_BindingOneProperty_AcrossAReplacedDataSource() => AssertMatches(Bind(
        ("SharedScopeMovie", @"<Prefab><Window><Widget><Children><SharedScopePart DataSource=""{Child}"" IsVisible=""@IsOn"" /></Children></Widget></Window></Prefab>"),
        ("SharedScopePart", @"<Prefab><Window><Widget IsVisible=""@IsOn""><Children><LabelWidget Label=""@Title"" /></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies that data source parameters with empty default values bind to the surrounding scope.
    /// </summary>
    [Test]
    public void ADataSourceParameterWhoseDefaultIsEmpty() => AssertMatches(Bind(
        ("EmptyScopeMovie", @"<Prefab><Window><Widget><Children><EmptyScopePart /></Children></Widget></Window></Prefab>"),
        ("EmptyScopePart", @"<Prefab><Parameters><Parameter Name=""HintDataSource"" DefaultValue="""" /></Parameters><Window><Widget><Children><ButtonWidget DataSource=""*HintDataSource"" Command.Click=""ExecuteBeginHint""><Children><LabelWidget Label=""@Title"" /></Children></ButtonWidget><ButtonWidget Command.Click=""ExecuteBeginHint""><Children><LabelWidget Label=""@Title"" /></Children></ButtonWidget></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies parameter inheritance across nested prefabs when intermediate components omit parameter forwarding.
    /// </summary>
    [Test]
    public void AParameterReachesThePrefabsNestedBelowTheOneItWasGivenTo() => AssertMatches(Bind(
        ("LeakedParameterMovie", @"<Prefab><Window><Widget><Children><LeakedParameterOuter Parameter.HintDataSource=""{Hint}"" /></Children></Widget></Window></Prefab>"),
        ("LeakedParameterOuter", @"<Prefab><Parameters><Parameter Name=""HintDataSource"" DefaultValue="""" /></Parameters><Window><Widget><Children><LeakedParameterInner /></Children></Widget></Window></Prefab>"),
        ("LeakedParameterInner", @"<Prefab><Parameters><Parameter Name=""HintDataSource"" DefaultValue="""" /></Parameters><Window><Widget><Children><ButtonWidget DataSource=""*HintDataSource"" Command.Click=""ExecuteBeginHint""><Children><LabelWidget Label=""@Title"" /></Children></ButtonWidget><ButtonWidget Command.Click=""ExecuteBeginHint""><Children><LabelWidget Label=""@Title"" /></Children></ButtonWidget></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies compilation and binding of data source paths containing newline characters within parameter defaults.
    /// </summary>
    [Test]
    public void ADataSourcePathWithALineBreak() => AssertMatches(Bind(
        ("LineBreakPathMovie", @"<Prefab><Window><Widget><Children><LineBreakPathPart /></Children></Widget></Window></Prefab>"),
        ("LineBreakPathPart", @"<Prefab><Parameters><Parameter Name=""P1"" DefaultValue=""line&#10;break"" /></Parameters><Window><FragileSetterWidget DataSource=""*P1"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies that self-referential cyclic constant definitions are detected and rejected rather than causing stack overflows.
    /// </summary>
    [Test]
    public void AConstantThatRefersToItself_IsDeclinedRatherThanResolved()
    {
        using var workspace = new PrefabWorkspace(("SelfConstantMovie",
            @"<Prefab><Constants><Constant Name=""C1"" Value=""!C2"" /><Constant Name=""C2"" Additive=""!C1"" Value=""1"" /></Constants><Window><Widget SuggestedWidth=""!C1"" /></Window></Prefab>"));

        var failure = Assert.Throws<InvalidOperationException>(() => workspace.Generate("SelfConstantMovie", typeof(TaleWorlds.Library.ViewModel)));

        Assert.That(failure!.Message, Does.Contain("refers to itself"));
    }

    [Test]
    public void ARootDataSource_FromAPrefabTheUsedOneIsRootedIn() => AssertMatches(Bind(
        ("ChainedScopeMovie", @"<Prefab><Window><Widget><Children><ChainedScopeOuter Label=""@Label"" /></Children></Widget></Window></Prefab>"),
        ("ChainedScopeOuter", @"<Prefab><Window><ChainedScopeInner /></Window></Prefab>"),
        ("ChainedScopeInner", @"<Prefab><Window><LabelWidget DataSource=""{Other}"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies that populating a list with a designated <c>Last</c> item template rebuilds preceding terminal items sequentially.
    /// </summary>
    [Test]
    public void AListFilledUnderALastItemTemplate_RebuildsEachPreviousLastItem() => AssertMatches(Bind(
        ("LastTemplateFillMovie", @"<Prefab><Window><Widget><Children><ListPanel DataSource=""{Items}""><ItemTemplate><ButtonWidget Id=""Default"" Command.Click=""ExecuteA""><Children><LabelWidget Label=""@Title"" /></Children></ButtonWidget></ItemTemplate><ItemTemplate Type=""Last""><LabelWidget Id=""Last"" Label=""@Title"" /></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies item template transitions when both <c>First</c> and <c>Last</c> item templates are configured.
    /// </summary>
    [Test]
    public void AListUnderAFirstAndALastItemTemplate_KeepsTheFirstTemplateOnlyForOneItem() => AssertMatches(Bind(
        ("FirstLastTemplateMovie", @"<Prefab><Window><Widget><Children><ListPanel DataSource=""{Items}""><ItemTemplate><LabelWidget Id=""Default"" Label=""@Title"" /></ItemTemplate><ItemTemplate Type=""First""><LabelWidget Id=""First"" Label=""@Title"" /></ItemTemplate><ItemTemplate Type=""Last""><LabelWidget Id=""Last"" Label=""@Title"" /></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies parent navigation (<c>{..}</c>) to an enclosing list when a child scope data source is replaced.
    /// </summary>
    [Test]
    public void AListReachedBackOutOfAReplacedChild() => AssertMatches(Bind(
        ("ParentScopeListFuzzMovie", @"<Prefab><Window><Widget><Children><Widget DataSource=""{Child}""><Children><LabelWidget Label=""@Title"" /><Widget DataSource=""{..}""><Children><ListPanel DataSource=""{Items}""><ItemTemplate><ButtonWidget Command.Click=""ExecuteA""><Children><CountingLabelWidget Label=""@Title"" /></Children></ButtonWidget></ItemTemplate></ListPanel></Children></Widget></Children></Widget></Children></Widget></Window></Prefab>")));

    /// <summary>
    /// Verifies attribute application order for children passed into a logical children location.
    /// </summary>
    [Test]
    public void ChildrenPassedIntoALogicalLocation_GetTheirAttributesAtTheLocation() => AssertMatches(Build(
        ("LogicalOrderMovie", @"<Prefab><Window><FragileSetterWidget><Children><LogicalOrderPart><Children><ListPanel ExtendRight="""" /></Children></LogicalOrderPart></Children></FragileSetterWidget></Window></Prefab>"),
        ("LogicalOrderPart", @"<Prefab><Window><BrushWidget><Children><ButtonWidget><LogicalChildrenLocation /></ButtonWidget><ReferenceWidget Rotation=""abc"" /></Children></BrushWidget></Window></Prefab>")));

    /// <summary>
    /// Verifies attribute application and data binding order for children passed into a logical children location.
    /// </summary>
    [Test]
    public void ChildrenPassedIntoALogicalLocation_GetTheirAttributesAtTheLocation_Bound() => AssertMatches(Bind(
        ("LogicalOrderBoundMovie", @"<Prefab><Window><FragileSetterWidget><Children><LogicalOrderBoundPart><Children><LabelWidget Label=""@Title"" /><ListPanel ExtendRight="""" /></Children></LogicalOrderBoundPart></Children></FragileSetterWidget></Window></Prefab>"),
        ("LogicalOrderBoundPart", @"<Prefab><Window><BrushWidget><Children><ButtonWidget><Children><LabelWidget Label=""inside"" /></Children><LogicalChildrenLocation /></ButtonWidget><ReferenceWidget Rotation=""abc"" /></Children></BrushWidget></Window></Prefab>")));

    /// <summary>
    /// Verifies property write-back behavior upon widget removal during movie release.
    /// </summary>
    [Test]
    public void AResetWrittenBackOnRelease() => AssertMatches(Bind(
        ("ResetOnReleaseMovie", @"<Prefab><Window><ListPanel><Children><ResetOnReleasePart /></Children></ListPanel></Window></Prefab>"),
        ("ResetOnReleasePart", @"<Prefab><Window><Widget GamepadNavigationIndex=""@Title"" /></Window></Prefab>")));

    /// <summary>
    /// Verifies property write-back behavior upon removing an individual list item widget from the tree.
    /// </summary>
    [Test]
    public void AResetWrittenBackOnRemovingAListItem() => AssertMatches(Bind(
        ("ResetOnRemovalMovie", @"<Prefab><Window><Widget><Children><ListPanel DataSource=""{Items}""><ItemTemplate><Widget GamepadNavigationIndex=""@Title"" /></ItemTemplate></ListPanel></Children></Widget></Window></Prefab>")));
}
