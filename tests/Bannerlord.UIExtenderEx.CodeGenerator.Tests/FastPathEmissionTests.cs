using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using NUnit.Framework;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// Which by-name bindings the generator gives a typed widget assignment and which keep
/// <c>WidgetExtensions.SetWidgetAttribute</c>, read off the generated file.
/// <para>
/// These say what was emitted, which is the half that can be read. What the emitted code <em>does</em> is
/// <see cref="TypedPayloadFastPathTests"/>, and neither is worth much without the other: a guard that compiles and
/// behaves differently from the call it replaced is exactly the failure this whole shape is exposed to.
/// </para>
/// </summary>
public class FastPathEmissionTests
{
    private const string MovieName = "FastPathEmissionMovie";

    private const string Prefab = @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <FastPathWidget Id=""Bool"" Flag=""@BoolValue"" />
        <FastPathWidget Id=""Int"" Number=""@IntValue"" />
        <FastPathWidget Id=""Widened"" Ratio=""@IntValue"" />
        <FastPathWidget Id=""Narrowed"" Number=""@DoubleValue"" />
        <FastPathWidget Id=""Sprite"" Picture=""@TextValue"" />
        <FastPathWidget Id=""ReadOnly"" ReadOnlyFlag=""@BoolValue"" />
        <FastPathWidget Id=""InternalSetter"" InternalSetterFlag=""@BoolValue"" />
        <FastPathWidget Id=""Nullable"" MaybeFlag=""@BoolValue"" />
        <FastPathWidget Id=""Indexer"" Item=""@BoolValue"" />
        <FastPathWidget Id=""Dotted"" Branch.Margin=""@IntValue"" />
        <FastPathWidget Id=""Absent"" NoSuchWidgetProperty=""@IntValue"" />
      </Children>
    </Widget>
  </Window>
</Prefab>";

    private PrefabWorkspace _workspace = null!;


    [SetUp]
    public void SetUp() => _workspace = new PrefabWorkspace((MovieName, Prefab));

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    private GeneratedCode Generate() => new(string.Join("\n", _workspace.Generate(MovieName, typeof(FastPathRootVM)).Select(x => x.Content)));

    /// <summary>The whole file between one handler's opening line and the next method's, so a claim can be made about one overload.</summary>
    private static GeneratedCode Handler(GeneratedCode code, string variant)
    {
        var lines = code.Text.Split('\n');
        var start = Array.FindIndex(lines, x => x.Contains($"HandleViewModelPropertyChangeWith{variant}ValueOf_datasource_Root(global::System.String"));
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"no {variant} payload handler was emitted");
        var end = Array.FindIndex(lines, start + 1, x => x.TrimStart().StartsWith("private ", StringComparison.Ordinal));
        return new GeneratedCode(string.Join("\n", lines.Skip(start).Take((end < 0 ? lines.Length : end) - start)));
    }

    // --- the eligible cases ----------------------------------------------------------------------------------------

    [Test]
    public void AnEligibleBoolBinding_IsAssignedDirectlyInTheBoolOverload()
    {
        var handler = Handler(Generate(), "Bool");

        Assert.That(handler.HasStatement("_widget_0.Flag = value;"), Is.True,
            "the payload is already the widget property's type, so it is assigned as it stands");
        Assert.That(handler.HasStatement("uiExtenderExAssigned__widget_0_Flag"), Is.False, "and needs no local to hold it");
        Assert.That(handler.HasStatement("SetWidgetAttribute(this.Context, _widget_0,"), Is.False,
            "the eligible binding needs no reflection in the overload that carries its own type");
    }

    /// <summary>Other bindings in the same overload still do, which is why the assertion above is per binding.</summary>
    [Test]
    public void TheBoolOverload_StillReflectsForEveryOtherBinding()
    {
        var handler = Handler(Generate(), "Bool");

        Assert.That(handler.HasStatement("SetWidgetAttribute(this.Context, _widget_5, \"ReadOnlyFlag\", value)"), Is.True);
        Assert.That(handler.HasStatement("SetWidgetAttribute(this.Context, _widget_7, \"MaybeFlag\", value)"), Is.True);
    }

    [Test]
    public void AWideningPair_IsAssignedWithTheCastTheBinderWouldHaveMade()
    {
        var handler = Handler(Generate(), "Int");

        Assert.That(handler.HasStatement("global::System.Single uiExtenderExAssigned__widget_2_Ratio = (global::System.Single)value;"), Is.True);
        Assert.That(handler.HasStatement("_widget_2.Ratio = uiExtenderExAssigned__widget_2_Ratio;"), Is.True);
    }

    [Test]
    public void TheObjectOverload_GuardsOnTheWidgetPropertyTypeAndFallsBackToReflection()
    {
        var handler = Handler(Generate(), "");

        Assert.That(handler.HasStatement("if (value is global::System.Boolean uiExtenderExAssigned__widget_0_Flag)"), Is.True);
        Assert.That(handler.HasStatement("_widget_0.Flag = uiExtenderExAssigned__widget_0_Flag;"), Is.True);
        Assert.That(handler.HasStatement("SetWidgetAttribute(this.Context, _widget_0, \"Flag\", value)"), Is.True,
            "and the else-branch keeps the call for everything the guard does not match");
    }

    [Test]
    public void EverySetterAssignment_IsWrappedTheWayReflectionWouldHaveWrappedIt()
    {
        var handler = Handler(Generate(), "Bool");

        Assert.That(handler.HasStatement("catch (global::System.Exception uiExtenderExSetterFailure__widget_0_Flag)"), Is.True);
        Assert.That(handler.HasStatement("throw new global::System.Reflection.TargetInvocationException(uiExtenderExSetterFailure__widget_0_Flag);"), Is.True);
    }

    // --- the rejected cases ----------------------------------------------------------------------------------------

    [Test]
    public void ARejectedNumericPair_KeepsReflection()
    {
        // A double does not fit an int parameter and the binder does not narrow, so the loader throws on this one and
        // the generated code has to throw the same way rather than quietly truncating
        var handler = Handler(Generate(), "Double");

        Assert.That(handler.HasStatement("SetWidgetAttribute(this.Context, _widget_3, \"Number\", value)"), Is.True);
        Assert.That(handler.HasStatement("_widget_3.Number ="), Is.False);
    }

    /// <summary>There is no string notification variant, so a string arrives boxed in the object overload and stays there.</summary>
    [Test]
    public void AStringHeadedForAConvertedProperty_KeepsTheLoadersConversion()
    {
        var code = Generate();

        Assert.That(code.HasStatement("HandleViewModelPropertyChangeWithStringValueOf"), Is.False, "the game raises no such notification");
        Assert.That(Handler(code, "").HasStatement("SetWidgetAttribute(this.Context, _widget_4, \"Picture\", value)"), Is.True);
    }

    [TestCase("_widget_5", "ReadOnlyFlag", TestName = "AWidgetPropertyWithNoPublicSetter_KeepsReflection")]
    [TestCase("_widget_6", "InternalSetterFlag", TestName = "AWidgetPropertyWithANonPublicSetter_KeepsReflection")]
    [TestCase("_widget_7", "MaybeFlag", TestName = "ANullableWidgetProperty_KeepsReflection")]
    [TestCase("_widget_9", "Branch.Margin", TestName = "ADottedPath_KeepsReflection")]
    public void AnIneligibleTarget_KeepsReflection(string widgetVariable, string widgetProperty)
    {
        var code = Generate();

        Assert.That(Handler(code, "").HasStatement($"SetWidgetAttribute(this.Context, {widgetVariable}, \"{widgetProperty}\", value)"), Is.True);
        Assert.That(code.HasStatement($"{widgetVariable}.{widgetProperty} = uiExtenderExAssigned"), Is.False);
    }

    /// <summary>
    /// A name no widget property answers. There is nothing to assign to and nothing to name, so the call stays and does
    /// what it has always done with it, which is nothing.
    /// </summary>
    [Test]
    public void AWidgetPropertyTheTypeDoesNotHave_KeepsReflection()
    {
        var code = Generate();

        Assert.That(Handler(code, "").HasStatement("SetWidgetAttribute(this.Context, _widget_10, \"NoSuchWidgetProperty\", value)"), Is.True);
    }

    /// <summary>
    /// <c>Item</c> is the name reflection gives an indexer, so a prefab can name one by accident. The generator treats
    /// it as a property the widget does not have: the loader's call stays, and - this is the part that mattered - the
    /// widget half of the binding is not emitted as <c>_widget_8.Item</c>, which does not compile and would have taken
    /// the whole movie back to XML.
    /// </summary>
    [Test]
    public void AnIndexer_IsTreatedAsNoPropertyAtAll()
    {
        var code = Generate();

        Assert.That(Handler(code, "").HasStatement("SetWidgetAttribute(this.Context, _widget_8, \"Item\", value)"), Is.True);
        Assert.That(code.HasStatement("_widget_8.Item"), Is.False);
    }

    // --- the by-name read ------------------------------------------------------------------------------------------

    /// <summary>
    /// An eligible by-name read is one lookup into a local, a test that both proves the widget property's own type and
    /// names the value, and the typed assignment behind it. The same shape the typed-notification handler uses for a
    /// payload, over a value that came from the loader instead.
    /// </summary>
    [Test]
    public void AnEligibleByNameRead_TestsTheValueAndAssignsItTyped()
    {
        var code = Generate();

        Assert.That(code.HasStatement("object uiExtenderExValue__widget_0_Flag = global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember"
            + ".Get(_datasource_Root, \"BoolValue\");"), Is.True);
        Assert.That(code.HasStatement("if (uiExtenderExValue__widget_0_Flag is global::System.Boolean uiExtenderExAssigned__widget_0_Flag)"), Is.True);
        Assert.That(code.HasStatement("= (global::System.Boolean)uiExtenderExValue__widget_0_Flag;"), Is.False, "the test already proved the type");
        Assert.That(code.HasStatement("if (uiExtenderExValue__widget_1_Number is global::System.Int32 uiExtenderExAssigned__widget_1_Number)"), Is.True);
        Assert.That(code.HasStatement("if (uiExtenderExValue__widget_2_Ratio is global::System.Single uiExtenderExAssigned__widget_2_Ratio)"), Is.True);
    }

    /// <summary>
    /// Both read sites read once and hand the one value to whichever branch takes it: the initial assignment and the
    /// rereading handler. Reading twice would be two calls into the instance for one assignment.
    /// </summary>
    [Test]
    public void BothReadSites_ReadOnceAndFallBackToTheBinder()
    {
        var code = Generate();

        Assert.That(code.Statements.Count(x => x.Contains("object uiExtenderExValue__widget_0_Flag = ")),
            Is.EqualTo(2), "once when the data source is assigned and once from the rereading handler");
        Assert.That(code.Statements.Count(x => x.Contains("SetWidgetAttribute(this.Context, _widget_0, \"Flag\", uiExtenderExValue__widget_0_Flag)")),
            Is.EqualTo(2), "and each one falls back to the binder with the value it already read");
    }

    /// <summary>
    /// A widget's writeback listens only to the notifications its class raises under a property it binds (WidgetRaises):
    /// Flag announces through the bool one, MaybeFlag through the object one, and Number announces nothing, so in XML it
    /// is never written back and here it has no listener and no handler at all.
    /// </summary>
    [Test]
    public void TheWriteback_ListensOnlyToWhatTheWidgetClassRaises()
    {
        var code = Generate();

        Assert.That(code.HasStatement("_widget_0.boolPropertyChanged += boolPropertyChangedListenerOf_widget_0;"), Is.True, "Flag, bool");
        Assert.That(code.CountStatements("_widget_0.") > 0 && !code.HasStatement("_widget_0.PropertyChanged += PropertyChangedListenerOf_widget_0;")
                    && !code.HasStatement("_widget_0.floatPropertyChanged += floatPropertyChangedListenerOf_widget_0;"), Is.True, "and nothing else");
        Assert.That(code.HasStatement("_widget_7.PropertyChanged += PropertyChangedListenerOf_widget_7;"), Is.True, "MaybeFlag, object");
        Assert.That(code.HasStatement("_widget_7.boolPropertyChanged += boolPropertyChangedListenerOf_widget_7;"), Is.False, "a nullable is not announced as a bool");
        Assert.That(code.CountStatements("private void HandleWidgetPropertyChangeOf_widget_1("), Is.EqualTo(0), "Number writes nothing back");
        Assert.That(code.HasStatement("_widget_1.intPropertyChanged += intPropertyChangedListenerOf_widget_1;"), Is.False);
    }

    [Test]
    public void TheWriteback_GoesByName()
    {
        var code = Generate();

        Assert.That(code.HasStatement("DynamicMember.Set(_datasource_Root, \"BoolValue\", value);"), Is.True);
        Assert.That(code.CountStatements("DynamicMember.Set(_datasource_Root, \"BoolValue\", value);") >= 2, Is.True,
            "eligible or not, a write by name is the same call");
    }

    /// <summary>
    /// The bindings the widget fast path declines have nothing to test the value against - a dotted path and an absent
    /// widget property have no target type, a nullable and a setterless property are not assignments typed code can
    /// make - so the read goes straight into <c>SetWidgetAttribute</c> with no local at all.
    /// </summary>
    [TestCase("_widget_5", "ReadOnlyFlag", "BoolValue", TestName = "AWidgetPropertyWithNoPublicSetter_IsAssignedByTheBinder")]
    [TestCase("_widget_7", "MaybeFlag", "BoolValue", TestName = "ANullableWidgetProperty_IsAssignedByTheBinder")]
    [TestCase("_widget_9", "Branch.Margin", "IntValue", TestName = "ADottedPath_IsAssignedByTheBinder")]
    [TestCase("_widget_10", "NoSuchWidgetProperty", "IntValue", TestName = "AWidgetPropertyTheTypeDoesNotHave_IsAssignedByTheBinder")]
    public void AnIneligibleTarget_IsAssignedByTheBinder(string widgetVariable, string widgetProperty, string boundName)
    {
        var code = Generate();

        Assert.That(code.HasStatement($"uiExtenderExValue_{widgetVariable}_{widgetProperty}"), Is.False, "no local, because nothing tests it");
        Assert.That(code.HasStatement($"SetWidgetAttribute(this.Context, {widgetVariable}, \"{widgetProperty}\", "
            + $"global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.Get(_datasource_Root, \"{boundName}\"));"), Is.True);
    }

    // --- shape ----------------------------------------------------------------------------------------------------

    /// <summary>A bool is neither, and a test for it in a bool-typed overload would not even compile.</summary>
    [Test]
    public void AValueTypedOverload_DoesNotTestForAViewModelOrAList()
    {
        var handler = Handler(Generate(), "Bool");

        Assert.That(handler.HasStatement("TaleWorlds.Library.ViewModel"), Is.False);
        Assert.That(handler.HasStatement("TaleWorlds.Library.IMBBindingList"), Is.False);
    }

    [Test]
    public void TheObjectOverload_StillDeclinesAChildDataSource()
    {
        var handler = Handler(Generate(), "");

        Assert.That(handler.HasStatement("if (value is global::TaleWorlds.Library.ViewModel || value is global::TaleWorlds.Library.IMBBindingList)"), Is.True);
    }

    [Test]
    public void EveryPayloadVariant_GetsAnOverloadOfItsOwnTakingItsOwnType()
    {
        var code = Generate();

        Assert.That(code.HasStatement("HandleViewModelPropertyChangeWithValueOf_datasource_Root(global::System.String propertyName, global::System.Object value)"), Is.True);
        Assert.That(code.HasStatement("HandleViewModelPropertyChangeWithBoolValueOf_datasource_Root(global::System.String propertyName, global::System.Boolean value)"), Is.True);
        Assert.That(code.HasStatement("HandleViewModelPropertyChangeWithIntValueOf_datasource_Root(global::System.String propertyName, global::System.Int32 value)"), Is.True);
        Assert.That(code.HasStatement("HandleViewModelPropertyChangeWithFloatValueOf_datasource_Root(global::System.String propertyName, global::System.Single value)"), Is.True);
        Assert.That(code.HasStatement("HandleViewModelPropertyChangeWithUIntValueOf_datasource_Root(global::System.String propertyName, global::System.UInt32 value)"), Is.True);
        Assert.That(code.HasStatement("HandleViewModelPropertyChangeWithColorValueOf_datasource_Root(global::System.String propertyName, global::TaleWorlds.Library.Color value)"), Is.True);
        Assert.That(code.HasStatement("HandleViewModelPropertyChangeWithDoubleValueOf_datasource_Root(global::System.String propertyName, global::System.Double value)"), Is.True);
        Assert.That(code.HasStatement("HandleViewModelPropertyChangeWithVec2ValueOf_datasource_Root(global::System.String propertyName, global::TaleWorlds.Library.Vec2 value)"), Is.True);
    }

    /// <summary>Each listener calls the overload for its own payload, which is what keeps the value out of a box.</summary>
    [Test]
    public void EachListener_CallsTheOverloadForItsOwnPayload()
    {
        var code = Generate();

        Assert.That(code.HasStatement("if (HandleViewModelPropertyChangeWithBoolValueOf_datasource_Root(e.PropertyName, e.Value))"), Is.True);
        Assert.That(code.HasStatement("if (HandleViewModelPropertyChangeWithVec2ValueOf_datasource_Root(e.PropertyName, e.Value))"), Is.True);
    }

    /// <summary>
    /// Five widgets bind <c>IntValue</c> in one handler and three of them declare locals. Same names, sibling blocks,
    /// and the compiler is the only thing that can say whether that was a scope or a collision.
    /// </summary>
    [Test]
    public void TheGeneratedCode_Compiles()
    {
        var compiler = new RoslynCompiler();

        var references = PrefabReferenceSet.CollectPaths(_workspace.WidgetFactory, typeof(FastPathRootVM));
        var sources = _workspace.Generate(MovieName, typeof(FastPathRootVM))
            .Concat([IgnoresAccessChecksSource.Create(references)])
            .ToList();

        var result = compiler.Compile(
            CompiledPrefabManager.GetAssemblyName(MovieName, typeof(FastPathRootVM), "testbuild0000000"), sources, references);

        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
    }

    // --- the whole output ----------------------------------------------------------------------------------------

    /// <summary>Every file generated for the movie, line for line; see <see cref="GeneratedSnapshot"/>.</summary>
    [Test]
    public Task Snapshot() => GeneratedSnapshot.Verify(_workspace.Generate(MovieName, typeof(FastPathRootVM)));
}
