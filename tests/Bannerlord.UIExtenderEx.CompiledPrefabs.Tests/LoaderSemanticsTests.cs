using Bannerlord.UIExtenderEx.Extensions;

using NUnit.Framework;

using System;
using System.Reflection;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests XML loader data binding semantics against the TaleWorlds UI framework.
/// <para>
/// Establishes baseline behavioral invariants for property access, widget attribute assignment, path traversal,
/// dynamic member tables, command execution, and property change notifications to ensure compiled prefab fallback parity.
/// </para>
/// </summary>
public class LoaderSemanticsTests
{
    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;

    [SetUp]
    public void SetUp()
    {
        _workspace = new PrefabWorkspace();
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(LoaderSemanticsTests));
    }

    [TearDown]
    public void TearDown()
    {
        _ui.Dispose();
        _workspace.Dispose();
    }

    private Widget NewWidget() => new(_ui.Context);

    // ---------------------------------------------------------------- scalar reads

    [Test]
    public void GetPropertyValue_OfANameTheTypeDoesNotHave_IsNull()
    {
        Assert.That(new LoaderBaseVM().GetPropertyValue("NoSuchProperty"), Is.Null);
    }

    /// <summary>
    /// Verifies that querying a property that evaluates to null returns null, matching missing property query results.
    /// </summary>
    [Test]
    public void GetPropertyValue_OfAPresentNullProperty_IsAlsoNull()
    {
        Assert.That(new LoaderBaseVM().GetPropertyValue("NullText"), Is.Null);
    }

    [Test]
    public void GetPropertyValue_OfADeclaredProperty_InvokesTheGetter()
    {
        Assert.That(new LoaderBaseVM().GetPropertyValue("BaseText"), Is.EqualTo("base"));
        Assert.That(new LoaderBaseVM().GetPropertyValue("Count"), Is.EqualTo(7));
    }

    [Test]
    public void GetPropertyValue_OfASubclassProperty_ResolvesAgainstTheInstance()
    {
        LoaderBaseVM viewModel = new LoaderDerivedVM();

        Assert.That(viewModel.GetPropertyValue("DerivedText"), Is.EqualTo("derived"),
            "the binding table is built from the runtime type, not the declared one");
    }

    /// <summary>
    /// Verifies that accessing a non-public property via <see cref="ViewModel.GetPropertyValue"/> throws an exception.
    /// </summary>
    [Test]
    public void GetPropertyValue_OfANonPublicProperty_Throws()
    {
        Assert.That(() => new LoaderBaseVM().GetPropertyValue("HiddenText"), Throws.Exception);
    }

    [Test]
    public void GetPropertyValue_OfAPropertyWithANonPublicGetter_Throws()
    {
        Assert.That(() => new LoaderBaseVM().GetPropertyValue("PrivateGetterText"), Throws.Exception);
    }

    /// <summary>
    /// Verifies that getter exceptions invoked via reflection are wrapped in <see cref="TargetInvocationException"/>.
    /// </summary>
    [Test]
    public void GetPropertyValue_OfAThrowingGetter_ThrowsTheReflectionWrapper()
    {
        Assert.That(() => new LoaderBaseVM().GetPropertyValue("ThrowingText"),
            Throws.InstanceOf<TargetInvocationException>().With.InnerException.InstanceOf<InvalidOperationException>());
    }

    // ---------------------------------------------------------------- scalar writes

    [Test]
    public void SetPropertyValue_OfANameTheTypeDoesNotHave_DoesNothing()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(() => viewModel.SetPropertyValue("NoSuchProperty", "x"), Throws.Nothing);
    }

    [Test]
    public void SetPropertyValue_OfAPropertyWithNoPublicSetter_DoesNothing()
    {
        var viewModel = new LoaderBaseVM();

        viewModel.SetPropertyValue("ReadOnlyText", "changed");

        Assert.That(viewModel.ReadOnlyText, Is.EqualTo("readonly"));
    }

    [Test]
    public void SetPropertyValue_OfAWritableProperty_InvokesTheSetter()
    {
        var viewModel = new LoaderBaseVM();

        viewModel.SetPropertyValue("WritableText", "changed");

        Assert.That(viewModel.WritableText, Is.EqualTo("changed"));
    }

    [Test]
    public void SetPropertyValue_OfAThrowingSetter_ThrowsTheReflectionWrapper()
    {
        Assert.That(() => new LoaderBaseVM().SetPropertyValue("ThrowingSetterText", "x"),
            Throws.InstanceOf<TargetInvocationException>().With.InnerException.InstanceOf<InvalidOperationException>());
    }

    /// <summary>Verifies that setting an incompatible value type on a property throws an exception without type coercion.</summary>
    [Test]
    public void SetPropertyValue_WithAnIncompatibleValue_Throws()
    {
        Assert.That(() => new LoaderBaseVM().SetPropertyValue("WritableText", 17), Throws.Exception);
    }

    // ---------------------------------------------------------------- widget assignment

    /// <summary>
    /// Verifies that assigning null to a reference-typed widget property assigns null to the target.
    /// </summary>
    [Test]
    public void SetWidgetAttribute_WithNull_OnAReferenceProperty_AssignsNull()
    {
        var widget = NewWidget();
        widget.Id = "before";

        WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "Id", null);

        Assert.That(widget.Id, Is.Null);
    }

    /// <summary>
    /// Verifies that assigning null to a value-typed widget property applies the default value of the target type via reflection binder coercion.
    /// </summary>
    [Test]
    public void SetWidgetAttribute_WithNull_OnAValueProperty_AssignsTheDefault()
    {
        var widget = NewWidget();
        widget.IsVisible = true;
        widget.NinePatchTop = 9;

        WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "IsVisible", null);
        WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "NinePatchTop", null);

        Assert.That(widget.IsVisible, Is.False);
        Assert.That(widget.NinePatchTop, Is.Zero);
    }

    /// <summary>Verifies that setting a non-existent widget attribute leaves the widget in its initial state.</summary>
    [Test]
    public void SetWidgetAttribute_OfANameTheWidgetDoesNotHave_LeavesTheWidgetAlone()
    {
        var widget = NewWidget();
        widget.Id = "before";

        WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "NoSuchWidgetProperty", "x");

        Assert.That(widget.Id, Is.EqualTo("before"));
    }

    [Test]
    public void SetWidgetAttribute_ConvertsAStringIntoAnInt()
    {
        var widget = NewWidget();

        WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "NinePatchTop", "13");

        Assert.That(widget.NinePatchTop, Is.EqualTo(13));
    }

    [Test]
    public void SetWidgetAttribute_ConvertsAStringIntoAColor()
    {
        var widget = NewWidget();

        WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "Color", "#FF0000FF");

        Assert.That(widget.Color, Is.EqualTo(Color.ConvertStringToColor("#FF0000FF")));
    }

    /// <summary>Verifies that integer values widen into single-precision floating-point widget properties via the reflection binder.</summary>
    [Test]
    public void SetWidgetAttribute_WidensAnIntIntoAFloatProperty()
    {
        var widget = NewWidget();

        WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "MarginLeft", 3);

        Assert.That(widget.MarginLeft, Is.EqualTo(3f));
    }

    [Test]
    public void SetWidgetAttribute_WithAnIncompatibleValue_Throws()
    {
        var widget = NewWidget();

        Assert.That(() => WidgetExtensions.SetWidgetAttribute(_ui.Context, widget, "IsVisible", "true"), Throws.Exception);
    }

    // ---------------------------------------------------------------- path traversal

    [Test]
    public void GetViewModelAtPath_WithNoSubPath_IsTheViewModelItself()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(viewModel.GetViewModelAtPath(new BindingPath("Root")), Is.SameAs(viewModel));
    }

    [Test]
    public void GetViewModelAtPath_WalksAViewModelChild()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(viewModel.GetViewModelAtPath(new BindingPath("Root\\Child")), Is.SameAs(viewModel.Child));
    }

    [Test]
    public void GetViewModelAtPath_OfANullChild_IsNull()
    {
        var viewModel = new LoaderBaseVM { Child = null };

        Assert.That(viewModel.GetViewModelAtPath(new BindingPath("Root\\Child")), Is.Null);
    }

    [Test]
    public void GetViewModelAtPath_OfAMemberThatIsNeitherViewModelNorList_IsNull()
    {
        Assert.That(new LoaderBaseVM().GetViewModelAtPath(new BindingPath("Root\\Scalar")), Is.Null);
    }

    [Test]
    public void GetViewModelAtPath_OfAListWithNoIndex_IsTheList()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(viewModel.GetViewModelAtPath(new BindingPath("Root\\Children")), Is.SameAs(viewModel.Children));
    }

    [Test]
    public void GetViewModelAtPath_WalksANumericListIndex()
    {
        var viewModel = new LoaderBaseVM();
        viewModel.Children.Add(new LoaderChildVM());
        viewModel.Children.Add(new LoaderChildVM());

        Assert.That(viewModel.GetViewModelAtPath(new BindingPath("Root\\Children\\1")), Is.SameAs(viewModel.Children[1]));
    }

    [Test]
    public void GetViewModelAtPath_OfAnEmptyList_IsNull()
    {
        Assert.That(new LoaderBaseVM().GetViewModelAtPath(new BindingPath("Root\\Children\\0")), Is.Null);
    }

    [Test]
    public void GetViewModelAtPath_OfAnOutOfRangeIndex_IsNull()
    {
        var viewModel = new LoaderBaseVM();
        viewModel.Children.Add(new LoaderChildVM());

        Assert.That(viewModel.GetViewModelAtPath(new BindingPath("Root\\Children\\4")), Is.Null);
    }

    /// <summary>A non-numeric step into a list is a conversion failure, not a null. The generator must not emit one.</summary>
    [Test]
    public void GetViewModelAtPath_OfAMalformedListIndex_Throws()
    {
        var viewModel = new LoaderBaseVM();
        viewModel.Children.Add(new LoaderChildVM());

        Assert.That(() => viewModel.GetViewModelAtPath(new BindingPath("Root\\Children\\NotANumber")), Throws.Exception);
    }

    // ---------------------------------------------------------------- instance member tables

    /// <summary>
    /// Verifies that distinct instances of the same runtime type can resolve identical member names to different targets via dynamic registration.
    /// </summary>
    [Test]
    public void TwoInstancesOfOneType_CanResolveTheSameNameDifferently()
    {
        var first = new LoaderBaseVM();
        var second = new LoaderBaseVM();
        first.AddProperty("Registered", Bound(new LoaderRegisteredMemberSource("first")));
        second.AddProperty("Registered", Bound(new LoaderRegisteredMemberSource("second")));

        Assert.That(first.GetPropertyValue("Registered"), Is.EqualTo("first"));
        Assert.That(second.GetPropertyValue("Registered"), Is.EqualTo("second"));
    }

    [Test]
    public void ARegistrationOnOneInstance_LeavesTheOtherAlone()
    {
        var registered = new LoaderBaseVM();
        var untouched = new LoaderBaseVM();
        registered.AddProperty("Registered", Bound(new LoaderRegisteredMemberSource("value")));

        Assert.That(registered.GetPropertyValue("Registered"), Is.EqualTo("value"));
        Assert.That(untouched.GetPropertyValue("Registered"), Is.Null, "the shared table was copied, not modified");
    }

    /// <summary>Verifies that an initial missing property lookup resolves successfully after dynamic registration.</summary>
    [Test]
    public void AMissFollowedByARegistration_Resolves()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(viewModel.GetPropertyValue("Registered"), Is.Null);

        viewModel.AddProperty("Registered", Bound(new LoaderRegisteredMemberSource("late")));

        Assert.That(viewModel.GetPropertyValue("Registered"), Is.EqualTo("late"));
    }

    /// <summary>
    /// Verifies that dynamically registering a property replaces an existing declared property entry on the target instance.
    /// </summary>
    [Test]
    public void ARegistrationCanReplaceAnExistingEntry()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(viewModel.GetPropertyValue("BaseText"), Is.EqualTo("base"));

        viewModel.AddProperty("BaseText", Bound(new LoaderRegisteredMemberSource("replaced")));

        Assert.That(viewModel.GetPropertyValue("BaseText"), Is.EqualTo("replaced"));
    }

    [Test]
    public void ARegisteredWriteGoesToTheRegisteredReceiver()
    {
        var viewModel = new LoaderBaseVM();
        var source = new LoaderRegisteredMemberSource("before");
        viewModel.AddProperty("Registered", Bound(source));

        viewModel.SetPropertyValue("Registered", "after");

        Assert.That(source.Value, Is.EqualTo("after"));
    }

    // ---------------------------------------------------------------- commands

    [Test]
    public void ExecuteCommand_OfANameTheTypeDoesNotHave_DoesNothing()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(() => viewModel.ExecuteCommand("NoSuchCommand", []), Throws.Nothing);
        Assert.That(viewModel.CommandCalls, Is.Zero);
    }

    [Test]
    public void ExecuteCommand_FindsAPrivateMethod()
    {
        var viewModel = new LoaderBaseVM();

        viewModel.ExecuteCommand("ExecutePrivate", []);

        Assert.That(viewModel.CommandCalls, Is.EqualTo(1));
    }

    [Test]
    public void ExecuteCommand_FindsAnInheritedMethod()
    {
        var viewModel = new LoaderDerivedVM();

        viewModel.ExecuteCommand("ExecuteInherited", []);

        Assert.That(viewModel.CommandCalls, Is.EqualTo(1));
    }

    [Test]
    public void ExecuteCommand_FindsASubclassMethodThroughABaseTypedReference()
    {
        LoaderBaseVM viewModel = new LoaderDerivedVM();

        viewModel.ExecuteCommand("ExecuteDerived", []);

        Assert.That(((LoaderDerivedVM) viewModel).DerivedCommandCalls, Is.EqualTo(1));
    }

    /// <summary>Verifies that zero-parameter commands execute successfully even when passed extraneous event arguments.</summary>
    [Test]
    public void ExecuteCommand_RunsAZeroParameterMethodWithExtraArguments()
    {
        var viewModel = new LoaderBaseVM();

        viewModel.ExecuteCommand("ExecuteNoArguments", [new object(), "extra"]);

        Assert.That(viewModel.CommandCalls, Is.EqualTo(1));
    }

    [Test]
    public void ExecuteCommand_ConvertsAStringArgumentIntoAnInt()
    {
        var viewModel = new LoaderBaseVM();

        viewModel.ExecuteCommand("ExecuteWithInt", ["42"]);

        Assert.That(viewModel.LastIntArgument, Is.EqualTo(42));
    }

    /// <summary>Verifies that command execution fails without invoking the target when given incompatible argument types.</summary>
    [Test]
    public void ExecuteCommand_WithAnIncompatibleArgument_DoesNotRun()
    {
        var viewModel = new LoaderBaseVM();

        viewModel.ExecuteCommand("ExecuteWithString", [17]);

        Assert.That(viewModel.CommandCalls, Is.Zero);
    }

    [Test]
    public void ExecuteCommand_WithTheWrongArgumentCount_DoesNotRun()
    {
        var viewModel = new LoaderBaseVM();

        viewModel.ExecuteCommand("ExecuteWithString", ["a", "b"]);

        Assert.That(viewModel.CommandCalls, Is.Zero);
    }

    [Test]
    public void ExecuteCommand_PassesAViewModelArgumentThrough()
    {
        var viewModel = new LoaderBaseVM();
        var argument = new LoaderChildVM();

        viewModel.ExecuteCommand("ExecuteWithViewModel", [argument]);

        Assert.That(viewModel.LastViewModelArgument, Is.SameAs(argument));
    }

    /// <summary>Verifies that null arguments satisfy command parameter compatibility checks and execute successfully.</summary>
    [Test]
    public void ExecuteCommand_WithANullArgument_Runs()
    {
        var viewModel = new LoaderBaseVM();

        viewModel.ExecuteCommand("ExecuteWithViewModel", [null!]);

        Assert.That(viewModel.CommandCalls, Is.EqualTo(1));
        Assert.That(viewModel.LastViewModelArgument, Is.Null);
    }

    [Test]
    public void ExecuteCommand_OfAThrowingMethod_ThrowsTheReflectionWrapper()
    {
        Assert.That(() => new LoaderBaseVM().ExecuteCommand("ExecuteThrowing", []),
            Throws.InstanceOf<TargetInvocationException>().With.InnerException.InstanceOf<InvalidOperationException>());
    }

    /// <summary>Verifies that dynamically registered methods take precedence over declared methods of the same name.</summary>
    [Test]
    public void ExecuteCommand_PrefersARegisteredMethod()
    {
        var viewModel = new LoaderBaseVM();
        var source = new LoaderRegisteredMemberSource("value");
        viewModel.AddMethod("ExecuteNoArguments", BoundMethod(source));

        viewModel.ExecuteCommand("ExecuteNoArguments", ["!"]);

        Assert.That(viewModel.CommandCalls, Is.Zero, "the registered method replaced the declared one");
    }

    // ---------------------------------------------------------------- notifications

    /// <summary>
    /// Verifies that <see cref="ViewModel.PropertyChangedWithValue"/> carries an explicit payload that can diverge from the property getter.
    /// </summary>
    [Test]
    public void ATypedNotification_CarriesAPayloadThatNeedNotMatchTheGetter()
    {
        var viewModel = new LoaderBaseVM();
        string? seenName = null;
        object? seenValue = null;
        viewModel.PropertyChangedWithValue += (_, e) => { seenName = e.PropertyName; seenValue = e.Value; };

        viewModel.OnPropertyChangedWithValue("a value the getter does not return", "BaseText");

        Assert.That(seenName, Is.EqualTo("BaseText"));
        Assert.That(seenValue, Is.EqualTo("a value the getter does not return"));
        Assert.That(viewModel.GetPropertyValue("BaseText"), Is.EqualTo("base"));
    }

    [Test]
    public void AnOrdinaryNotification_CarriesOnlyAName()
    {
        var viewModel = new LoaderBaseVM();
        string? seenName = null;
        viewModel.PropertyChanged += (_, e) => seenName = e.PropertyName;

        viewModel.OnPropertyChanged("BaseFlag");

        Assert.That(seenName, Is.EqualTo("BaseFlag"));
    }

    private static PropertyInfo Bound(LoaderRegisteredMemberSource source) =>
        new Bannerlord.BUTR.Shared.Utils.WrappedPropertyInfo(
            typeof(LoaderRegisteredMemberSource).GetProperty(nameof(LoaderRegisteredMemberSource.Value))!, source);

    private static MethodInfo BoundMethod(LoaderRegisteredMemberSource source) =>
        new Bannerlord.BUTR.Shared.Utils.WrappedMethodInfo(
            typeof(LoaderRegisteredMemberSource).GetMethod(nameof(LoaderRegisteredMemberSource.Echo))!, source);
}
