using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;

using NUnit.Framework;

using System;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.UIExtenderEx.CodeGenerator.Tests;

/// <summary>
/// <see cref="WidgetRaises"/>: which change notifications a widget class can raise under a bound property's name, read off
/// its IL, and where that cannot be decided, all nine.
/// </summary>
[NonParallelizable]
public class WidgetRaisesTests
{
    private static readonly string[] All = ["", "bool", "float", "Vec2", "Vector2", "double", "int", "uint", "Color"];

    [TearDown]
    public void TearDown()
    {
        CodeGeneratorEnvironment.IsPatched = static _ => false;
        WidgetRaises.ClearCache();
    }

    private static string[] Needed(Type type, params string[] properties)
    {
        var needed = WidgetRaises.Needed(type, properties, out var undecided);
        Assert.That(undecided, Is.Null, "decided");
        return [.. needed];
    }

    [Test]
    public void AConstantName_SelectsTheNotificationItIsRaisedWith()
    {
        Assert.That(Needed(typeof(ConstantRaisesWidget), nameof(ConstantRaisesWidget.Flag)), Is.EqualTo(new[] { "bool" }), "CallerMemberName");
        Assert.That(Needed(typeof(ConstantRaisesWidget), "Literal"), Is.EqualTo(new[] { "float" }), "a literal name");
        Assert.That(Needed(typeof(ConstantRaisesWidget), nameof(ConstantRaisesWidget.Flag), "Literal"), Is.EqualTo(new[] { "bool", "float" }));
    }

    [Test]
    public void APropertyNeverRaised_NeedsNoNotification()
    {
        // The loader's view listens and finds nothing to write back; there is nothing to listen to
        Assert.That(Needed(typeof(ConstantRaisesWidget), nameof(ConstantRaisesWidget.Silent)), Is.Empty);
    }

    [Test]
    public void AValueAnnouncedAsAnotherType_IsTheNotificationOfThatType()
    {
        // As an alignment announces its name: the enum property raises the object notification, with a string
        Assert.That(Needed(typeof(ConstantRaisesWidget), nameof(ConstantRaisesWidget.Mode)), Is.EqualTo(new[] { "" }));
    }

    [Test]
    public void ARaiseOfABaseClass_OrOfALambda_IsTheWidgetsToo()
    {
        Assert.That(Needed(typeof(DerivedRaisesWidget), nameof(ConstantRaisesWidget.Flag)), Is.EqualTo(new[] { "bool" }), "inherited");
        Assert.That(Needed(typeof(DerivedRaisesWidget), "FromLambda"), Is.EqualTo(new[] { "int" }), "in a closure, a nested type");
    }

    [Test]
    public void AComputedName_KeepsItsNotificationForEveryProperty()
    {
        Assert.That(Needed(typeof(ComputedNameWidget), "Anything"), Is.EqualTo(new[] { "float" }));
        Assert.That(Needed(typeof(ComputedNameWidget), nameof(ComputedNameWidget.Flag)), Is.EqualTo(new[] { "bool", "float" }), "and the constant ones still count");
    }

    [Test]
    public void ADelegateOfAnOverload_KeepsItsNotificationForEveryProperty()
    {
        Assert.That(Needed(typeof(DelegateRaisesWidget), "Anything"), Is.EqualTo(new[] { "int" }));
    }

    [Test]
    public void APatchedMethodInTheHierarchy_KeepsAllNine()
    {
        CodeGeneratorEnvironment.IsPatched = static method => method.DeclaringType == typeof(ConstantRaisesWidget);

        var needed = WidgetRaises.Needed(typeof(DerivedRaisesWidget), [nameof(ConstantRaisesWidget.Flag)], out var undecided);

        Assert.That(needed, Is.EqualTo(All));
        Assert.That(undecided, Does.Contain("patched"));
        Assert.That(WidgetRaises.PatchedMethodOf(typeof(DerivedRaisesWidget)), Is.Not.Null, "the fingerprint sees it too");
    }

    [Test]
    public void AWidgetClassThatDidNotResolve_KeepsAllNine()
    {
        var needed = WidgetRaises.Needed(null, ["Flag"], out var undecided);

        Assert.That(needed, Is.EqualTo(All));
        Assert.That(undecided, Is.Not.Null);
    }
}

public enum RaisesMode
{
    First,
    Second,
}

public class ConstantRaisesWidget : Widget
{
    public ConstantRaisesWidget(UIContext context) : base(context) { }

    private bool _flag;

    public bool Flag
    {
        get => _flag;
        set
        {
            _flag = value;
            OnPropertyChanged(value);
        }
    }

    private float _literal;

    public float LiteralValue
    {
        get => _literal;
        set
        {
            _literal = value;
            OnPropertyChanged(value, "Literal");
        }
    }

    public int Silent { get; set; }

    private RaisesMode _mode;

    public RaisesMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            OnPropertyChanged(value.ToString());
        }
    }
}

public class DerivedRaisesWidget : ConstantRaisesWidget
{
    public DerivedRaisesWidget(UIContext context) : base(context) { }

    public Action<int> Raise => value => OnPropertyChanged(value, "FromLambda");
}

public class ComputedNameWidget : Widget
{
    public ComputedNameWidget(UIContext context) : base(context) { }

    private bool _flag;

    public bool Flag
    {
        get => _flag;
        set
        {
            _flag = value;
            OnPropertyChanged(value);
        }
    }

    /// <summary>A helper as mods write them: the name it raises with is whatever its caller passes.</summary>
    protected void SetAndRaise(float value, string name) => OnPropertyChanged(value, name);
}

public class DelegateRaisesWidget : Widget
{
    public DelegateRaisesWidget(UIContext context) : base(context) { }

    /// <summary>A delegate made of the int overload raises whatever name it is invoked with.</summary>
    public Action<int, string> Raiser => OnPropertyChanged;
}
