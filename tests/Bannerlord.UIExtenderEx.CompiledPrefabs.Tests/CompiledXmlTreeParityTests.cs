using Bannerlord.UIExtenderEx.Tests.Oracle;
using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;

using NSubstitute;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies attribute and structural parity between widget trees constructed via the GauntletUI XML loader
/// and widget trees instantiated through extender-compiled C# code.
/// <para>
/// Compares declared XML attribute assignments and widget hierarchies directly across both trees, ensuring
/// dropped or malformed attributes fail deterministically without relying on manual rendering checks.
/// </para>
/// </summary>
public class CompiledXmlTreeParityTests
{
    private PrefabWorkspace? _workspace;
    private SpriteData? _spriteData;
    private BrushFactory? _brushFactory;


    [TearDown]
    public void TearDown() => _workspace?.Dispose();

    /// <summary>
    /// Verifies parity for dotted attribute paths navigating through interface-declared properties.
    /// </summary>
    [Test]
    public void ADottedPathThroughABaseTypedProperty_ArrivesInBothTrees()
    {
        AssertParity("LayoutMovie", """
<Prefab>
  <Window>
    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" LayoutImp.LayoutMethod="VerticalTopToBottom" />
  </Window>
</Prefab>
""");
    }

    [Test]
    public void PlainLayoutAttributes_ArriveInBothTrees()
    {
        AssertParity("PlainMovie", """
<Prefab>
  <Window>
    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="40" MarginLeft="5" MarginTop="7" IsVisible="false">
      <Children>
        <Widget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Center" VerticalAlignment="Bottom" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""");
    }

    [Test]
    public void NestedDottedPaths_ArriveInBothTrees()
    {
        AssertParity("NestedMovie", """
<Prefab>
  <Window>
    <ListPanel LayoutImp.LayoutMethod="VerticalBottomToTop" WidthSizePolicy="StretchToParent">
      <Children>
        <ListPanel LayoutImp.LayoutMethod="HorizontalLeftToRight" HeightSizePolicy="CoverChildren" />
      </Children>
    </ListPanel>
  </Window>
</Prefab>
""");
    }

    // --- WidgetExtensions.SetWidgetAttributeFromString, value by value ----------------------------------------------

    /// <summary>
    /// Verifies that boolean attribute parsing matches XML loader semantics, where any value other than exact lowercase <c>"true"</c> yields <see langword="false"/>.
    /// </summary>
    [Test]
    public void ABoolWrittenInAnotherCase_IsFalseInBothTrees()
    {
        AssertParity("BoolMovie", """
<Prefab>
  <Window>
    <Widget IsVisible="True" DoNotAcceptEvents="TRUE" />
  </Window>
</Prefab>
""");
    }

    /// <summary>
    /// Verifies that valid conversions are applied and malformed values are safely rejected without terminating tree construction, matching XML loader error recovery.
    /// </summary>
    [Test]
    public void ValuesTheLoaderConvertsOrRejects_ArriveOrAreLeftAloneInBothTrees()
    {
        AssertParity("ConversionMovie", """
<Prefab>
  <Window>
    <Widget SuggestedWidth="" MarginLeft="5." MarginRight="1,000" MarginTop="1e2" HorizontalAlignment="2" VerticalAlignment="Nowhere" NinePatchTop="abc" NinePatchLeft=" 7" />
  </Window>
</Prefab>
""");
    }

    /// <summary>
    /// Verifies that attribute text containing quotes, backslashes, and line breaks is properly escaped in generated C# literals.
    /// </summary>
    [Test]
    public void TextWithQuotesBackslashesAndLineBreaks_ArrivesInBothTrees()
    {
        AssertParity("QuoteMovie", """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <LabelWidget Label="say &quot;hi&quot; to C:\temp\" />
        <LabelWidget Label="line one&#10;line two" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""");
    }

    /// <summary>
    /// Verifies that empty parameters and constants omit attribute assignments in parity with <c>WidgetTemplate.SetAttributes</c>, preserving default widget values.
    /// </summary>
    [Test]
    public void ParametersAndConstantsThatComeOutEmpty_AssignNothingInBothTrees()
    {
        var (_, fromXml, fromCompiled) = BuildBoth("EmptyParameterMovie", """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <EmptyParameterPart Id="Given" Parameter.Label="line one&#10;line two" Parameter.Width="40" />
        <EmptyParameterPart Id="NotGiven" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""", ("EmptyParameterPart", """
<Prefab>
  <Parameters>
    <Parameter Name="Label" DefaultValue="" />
    <Parameter Name="Width" DefaultValue="" />
  </Parameters>
  <Constants>
    <Constant Name="Nothing" Value="" />
  </Constants>
  <Window>
    <LabelWidget Label="*Label" SuggestedWidth="*Width" SuggestedHeight="!Nothing" IsVisible="*Undeclared" />
  </Window>
</Prefab>
"""));

        foreach (var id in new[] { "Given", "NotGiven" })
        {
            var xmlWidget = (LabelWidget) ById(fromXml, id);
            var compiledWidget = (LabelWidget) ById(fromCompiled, id);
            Assert.That(compiledWidget.Label, Is.EqualTo(xmlWidget.Label), id);
            Assert.That(compiledWidget.SuggestedWidth, Is.EqualTo(xmlWidget.SuggestedWidth), id);
            Assert.That(compiledWidget.SuggestedHeight, Is.EqualTo(xmlWidget.SuggestedHeight), id);
            Assert.That(compiledWidget.IsVisible, Is.EqualTo(xmlWidget.IsVisible), id);
        }
        Assert.That(((LabelWidget) ById(fromCompiled, "Given")).Label, Is.EqualTo("line one\nline two"));
        Assert.That(((LabelWidget) ById(fromCompiled, "NotGiven")).Label, Is.EqualTo("unset"), "an empty parameter leaves the widget's own value");
    }

    // --- Ids (WidgetTemplate.CreateWidgets) ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that widget identifier assignment rules match XML loader semantics across templates, nested prefabs, and parameterized IDs.
    /// </summary>
    [Test]
    public void EveryWidgetsId_IsTheOneTheLoaderGivesIt()
    {
        AssertParity("IdMovie", """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <IdPart />
        <IdPart Id="Outer" />
        <Widget />
        <Widget Id="Plain" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""", ("IdPart", """
<Prefab>
  <Window>
    <Widget Id="InnerRoot">
      <Children>
        <Widget Id="*NotAnId" />
      </Children>
    </Widget>
  </Window>
</Prefab>
"""));
    }

    // --- widget references ----------------------------------------------------------------------------------------

    /// <summary>
    /// Verifies that relative widget reference paths resolve over the instantiated widget tree, correctly handling logical children locations.
    /// </summary>
    [Test]
    public void AWidgetReference_IsResolvedOverTheTreeTheLoaderWalks()
    {
        AssertParity("ReferenceMovie", """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="Sibling" />
        <ScrollablePanel Id="ThroughLocation" InnerPanel="Holder\Inner" FixedHeader="..\Sibling" ClipRect="Holder\Slot">
          <Children>
            <HolderPart Id="Holder">
              <Children>
                <Widget Id="Inner" />
              </Children>
            </HolderPart>
          </Children>
        </ScrollablePanel>
        <ScrollablePanel Id="Direct" InnerPanel="Target" ClipRect="Nowhere\Deeper">
          <Children>
            <Widget Id="Target" />
          </Children>
        </ScrollablePanel>
      </Children>
    </Widget>
  </Window>
</Prefab>
""", ("HolderPart", """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="Slot">
          <LogicalChildrenLocation />
        </Widget>
      </Children>
    </Widget>
  </Window>
</Prefab>
"""));
    }

    // --- visual definitions and custom elements -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that visual definitions, sanitized names, and visual states are instantiated in parity with XML loader construction rules.
    /// </summary>
    [Test]
    public void VisualDefinitions_AreCreatedAsTheLoaderCreatesThem()
    {
        AssertParity("VisualMovie", """
<Prefab>
  <VisualDefinitions>
    <VisualDefinition Name="Tab.Left" TransitionDuration="0.25">
      <VisualState State="Default" PositionYOffset="1" MarginLeft="2.5" />
    </VisualDefinition>
    <VisualDefinition Name="Tab_Left" TransitionDuration="0.5">
      <VisualState State="Default" PositionYOffset="3" NoSuchAttribute="4" GotMarginTop="true" SuggestedWidth="wide" />
    </VisualDefinition>
  </VisualDefinitions>
  <Window>
    <Widget>
      <Children>
        <Widget VisualDefinition="Tab.Left" />
        <Widget VisualDefinition="Tab_Left" />
        <Widget VisualDefinition="Missing" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""");
    }

    /// <summary>
    /// Verifies that declared custom XML elements are passed to corresponding <see cref="XmlElement"/> widget properties.
    /// </summary>
    [Test]
    public void ACustomElement_ArrivesInBothTrees()
    {
        AssertParity("CustomElementMovie", """
<Prefab>
  <CustomElements>
    <CustomElement Name="Payload"><Data Kind="a &quot;quoted&quot; value"><Item Index="1" /></Data></CustomElement>
  </CustomElements>
  <Window>
    <Widget>
      <Children>
        <CustomElementWidget Custom="Payload" />
        <CustomElementWidget Custom="Missing" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""");
    }

    // --- the comparison ---------------------------------------------------------------------------------------------

    private WidgetFactory? _plainFactory;

    private void AssertParity(string movieName, string xml, params (string Name, string Xml)[] otherPrefabs)
    {
        var (prefab, fromXml, fromCompiled) = BuildBoth(movieName, xml, otherPrefabs);

        var mismatches = new List<string>();
        Compare(prefab.RootTemplate, fromXml, fromCompiled, fromXml, fromCompiled, "root", mismatches);
        Assert.That(mismatches, Is.Empty, string.Join(Environment.NewLine, mismatches));

        AssertOracleMatches(movieName);
    }

    /// <summary>
    /// Executes <see cref="LoaderOracle"/> to compare full widget properties and diagnostic assertion counts across XML and compiled trees.
    /// </summary>
    private OracleResult AssertOracleMatches(string movieName)
    {
        var oracle = new LoaderOracle(_plainFactory!, _spriteData!, _brushFactory!, CreateUIContext(), [typeof(LabelWidget).Assembly]);
        var result = oracle.Run([movieName]).Single();
        Assert.That(result.Outcome, Is.EqualTo(OracleOutcome.Match),
            $"{result.Detail}{Environment.NewLine}{string.Join(Environment.NewLine, result.Differences.Take(20))}");
        Assert.That(result.CompiledAsserts, Is.EqualTo(result.XmlAsserts), result.Detail);
        return result;
    }

    // --- failures the loader survives -------------------------------------------------------------------------------

    /// <summary>
    /// Verifies that property setter exceptions are caught and logged as non-fatal assertions, matching <c>SetWidgetAttributeFromString</c> error recovery.
    /// </summary>
    [Test]
    public void ASetterThatThrows_IsCaughtAndAssertedAsTheLoaderDoes()
    {
        BuildBoth("FragileMovie", """
<Prefab>
  <Window>
    <FragileSetterWidget Fragile="anything" After="reached" />
  </Window>
</Prefab>
""");

        var result = AssertOracleMatches("FragileMovie");

        Assert.That(result.XmlAsserts, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that referencing an undefined constant halts attribute assignment for that widget subtree while preserving outer movie construction.
    /// </summary>
    [Test]
    public void AMissingConstant_StopsTheWidgetAndEverythingBelowItAsTheLoaderDoes()
    {
        BuildBoth("MissingConstantMovie", """
<Prefab>
  <Constants>
    <Constant Name="Known" Value="5" />
  </Constants>
  <Window>
    <Widget>
      <Children>
        <Widget MarginLeft="!Known" MarginTop="!Unknown" MarginRight="7">
          <Children>
            <Widget MarginLeft="9" />
          </Children>
        </Widget>
        <Widget MarginLeft="11" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""");

        var result = AssertOracleMatches("MissingConstantMovie");

        Assert.That(result.XmlAsserts, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that rejected conversion values trigger loader assertions in parity with XML parsing.
    /// </summary>
    [Test]
    public void ARejectedValue_IsAssertedAsTheLoaderAssertsIt()
    {
        BuildBoth("RejectedMovie", """
<Prefab>
  <Window>
    <Widget SuggestedWidth="wide" NinePatchTop="abc" HorizontalAlignment="Nowhere" />
  </Window>
</Prefab>
""");

        var result = AssertOracleMatches("RejectedMovie");

        Assert.That(result.XmlAsserts, Is.EqualTo(3));
    }

    /// <summary>
    /// Instantiates the specified prefab through both the XML loader and compiled C# generation.
    /// </summary>
    private (WidgetPrefab Prefab, Widget FromXml, Widget FromCompiled) BuildBoth(string movieName, string xml, params (string Name, string Xml)[] otherPrefabs)
    {
        _workspace = new PrefabWorkspace([(movieName, xml), .. otherPrefabs]);
        _spriteData = new SpriteData(nameof(CompiledXmlTreeParityTests));
        _brushFactory = new BrushFactory(_workspace.ResourceDepot, "Brushes", _spriteData, new FontFactory(_workspace.ResourceDepot));

        // Construct a WidgetFactory without PrefabDatabindingExtension since static parity tests contain no ViewModel data bindings.
        _plainFactory = new WidgetFactory(_workspace.ResourceDepot, "Prefabs");
        _plainFactory.Initialize();

        var prefab = _plainFactory.GetCustomType(movieName);
        var fromXml = prefab.Instantiate(new WidgetCreationData(CreateUIContext(), _plainFactory)).Widget;
        var fromCompiled = InstantiateCompiled(movieName);
        return (prefab, fromXml, fromCompiled);
    }

    /// <summary>
    /// Recursively traverses the template hierarchy alongside both instantiated widget trees, verifying attribute values and widget IDs.
    /// <para>
    /// Transitions to direct live-tree traversal when encountering nested prefab instances to accommodate logical children locations.
    /// </para>
    /// </summary>
    private void Compare(WidgetTemplate template, Widget xmlWidget, Widget compiledWidget, Widget xmlRoot, Widget compiledRoot, string path, List<string> mismatches)
    {
        if (xmlWidget.Id != compiledWidget.Id)
            mismatches.Add($"{path}.Id: XML gives '{xmlWidget.Id ?? "<null>"}', compiled gives '{compiledWidget.Id ?? "<null>"}'");

        foreach (var attribute in template.AllAttributes)
        {
            // Skip ID attributes (handled separately) and unbound ViewModel expressions.
            if (attribute.KeyType is not WidgetAttributeKeyTypeAttribute || attribute.ValueType is not WidgetAttributeValueTypeDefault)
                continue;

            var xmlValue = Describe(ReadPath(xmlWidget, attribute.Key), xmlRoot);
            var compiledValue = Describe(ReadPath(compiledWidget, attribute.Key), compiledRoot);
            if (!Equals(xmlValue, compiledValue))
                mismatches.Add($"{path}.{attribute.Key}: XML gives '{xmlValue ?? "<unset>"}', compiled gives '{compiledValue ?? "<unset>"}'");
        }

        if (_plainFactory!.IsCustomType(template.Type))
        {
            CompareLive(xmlWidget, compiledWidget, path, mismatches);
            return;
        }

        if (xmlWidget.ChildCount != compiledWidget.ChildCount)
        {
            mismatches.Add($"{path}: {xmlWidget.ChildCount} children from XML, {compiledWidget.ChildCount} compiled");
            return;
        }
        for (var i = 0; i < xmlWidget.ChildCount && i < template.ChildCount; i++)
            Compare(template.GetChildAt(i), xmlWidget.GetChild(i), compiledWidget.GetChild(i), xmlRoot, compiledRoot, $"{path}[{i}]", mismatches);
    }

    /// <summary>
    /// Compares two live widget subtrees node-by-node, verifying identical widget types, IDs, and child counts.
    /// </summary>
    private static void CompareLive(Widget xmlWidget, Widget compiledWidget, string path, List<string> mismatches)
    {
        if (xmlWidget.Id != compiledWidget.Id)
            mismatches.Add($"{path}.Id: XML gives '{xmlWidget.Id ?? "<null>"}', compiled gives '{compiledWidget.Id ?? "<null>"}'");
        if (xmlWidget.ChildCount != compiledWidget.ChildCount)
        {
            mismatches.Add($"{path}: {xmlWidget.ChildCount} children from XML, {compiledWidget.ChildCount} compiled");
            return;
        }
        for (var i = 0; i < xmlWidget.ChildCount; i++)
            CompareLive(xmlWidget.GetChild(i), compiledWidget.GetChild(i), $"{path}[{i}]", mismatches);
    }

    /// <summary>
    /// Produces a comparable representation of attribute values across distinct widget trees.
    /// </summary>
    private static object? Describe(object? value, Widget root) => value switch
    {
        Widget widget => "widget at " + TreePath(widget, root),
        VisualDefinition definition => DescribeVisualDefinition(definition),
        XmlElement element => element.OuterXml,
        _ => value,
    };

    private static string TreePath(Widget widget, Widget root)
    {
        var steps = new List<string>();
        for (var current = widget; current != root; current = current.ParentWidget)
        {
            if (current.ParentWidget is not { } parent)
                return "<outside the tree>";
            steps.Insert(0, parent.GetChildIndex(current).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return "root" + string.Concat(steps.Select(x => $"[{x}]"));
    }

    private static string DescribeVisualDefinition(VisualDefinition definition)
    {
        var states = definition.VisualStates.Values.OrderBy(x => x.State, StringComparer.Ordinal).Select(state =>
            state.State + "{" + string.Join(", ", typeof(VisualState).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x => x.Name != nameof(VisualState.State))
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => $"{x.Name}={x.GetValue(state)}")) + "}");
        return $"{definition.Name} ({definition.TransitionDuration}): {string.Join("; ", states)}";
    }

    /// <summary>
    /// Evaluates a potentially dotted attribute property path against the target widget object.
    /// </summary>
    private static object? ReadPath(object target, string path)
    {
        object? current = target;
        foreach (var segment in path.Split('.'))
        {
            if (current is null)
                return null;
            var property = current.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
                return null;
            current = property.GetValue(current);
        }
        return current;
    }

    private static Widget ById(Widget root, string id) => FindById(root, id) ?? throw new InvalidOperationException($"No widget with Id '{id}'.");

    private static Widget? FindById(Widget widget, string id)
    {
        if (widget.Id == id)
            return widget;
        for (var i = 0; i < widget.ChildCount; i++)
        {
            if (FindById(widget.GetChild(i), id) is { } found)
                return found;
        }
        return null;
    }

    private Widget InstantiateCompiled(string movieName)
    {
        var compiler = new RoslynCompiler();

        var references = PrefabReferenceSet.CollectPaths(_workspace!.WidgetFactory, typeof(CodegenTestVM));
        var sources = _workspace.Generate(movieName, typeof(CodegenTestVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = compiler.Compile(CompiledPrefabManager.GetAssemblyName(movieName, typeof(CodegenTestVM), "treeparity000000"), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));

        var environment = new GameCompiledPrefabEnvironment();
        var assembly = environment.LoadAssembly(result.Assembly!);
        // Register generated widget classes with WidgetInfo to satisfy base Widget constructor lookup requirements.
        Assert.That(environment.CreateCreator(assembly), Is.Not.Null);

        var rootType = assembly.GetTypes().First(x => x.Name.StartsWith(movieName + "__", StringComparison.Ordinal));
        var widget = (Widget) Activator.CreateInstance(rootType, CreateUIContext())!;
        foreach (var name in new[] { "CreateWidgets", "SetIds", "SetAttributes" })
            rootType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(widget, []);
        return widget;
    }

    private UIContext CreateUIContext()
    {
        var platform = Substitute.For<ITwoDimensionPlatform>();
        platform.ReferenceHeight.Returns(1080f);
        platform.ReferenceWidth.Returns(1920f);
        var twoDimension = new TwoDimensionContext(platform, Substitute.For<ITwoDimensionResourceContext>(), _workspace!.ResourceDepot);
        TestInput.EnsureInitialized();
        var context = new UIContext(twoDimension, Substitute.For<TaleWorlds.InputSystem.IInputContext>(), _spriteData!, new FontFactory(_workspace.ResourceDepot), _brushFactory!);
        context.Initialize();
        return context;
    }
}
