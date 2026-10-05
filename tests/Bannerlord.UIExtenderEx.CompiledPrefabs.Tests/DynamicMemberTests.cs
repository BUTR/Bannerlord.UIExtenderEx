using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests dynamic member fallback operations. Verifies that <see cref="DynamicMember"/> mirrors loader resolution
/// semantics across property reads, writes, and command execution, while reporting diagnostics without suppressing
/// subsequently registered members.
/// </summary>
public class DynamicMemberTests
{
    private IDynamicMemberHost? _previousHost;

    [SetUp]
    public void SetUp()
    {
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        DynamicMember.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        DynamicMember.Reset();
    }

    // ---------------------------------------------------------------- reads

    [Test]
    public void Get_OfANameTheInstanceDoesNotHave_IsNull()
    {
        Assert.That(DynamicMember.Get(new LoaderBaseVM(), "NoSuchProperty"), Is.Null);
    }

    [Test]
    public void Get_OfANullOwner_IsNull()
    {
        Assert.That(DynamicMember.Get(null, "BaseText"), Is.Null);
    }

    [Test]
    public void Get_OfADeclaredProperty_ReadsIt()
    {
        Assert.That(DynamicMember.Get(new LoaderBaseVM(), "BaseText"), Is.EqualTo("base"));
    }

    /// <summary>Verifies that accessing an existing property containing null returns null without logging a binding miss.</summary>
    [Test]
    public void Get_OfAPresentNullProperty_IsNullAndIsNotAMiss()
    {
        var host = new RecordingHost();
        DynamicMember.Host = host;

        Assert.That(DynamicMember.Get(new LoaderBaseVM(), "NullText"), Is.Null);

        Assert.That(host.Messages, Is.Empty);
    }

    /// <summary>
    /// Verifies that property resolution targets the runtime derived instance when accessed via a base-typed reference.
    /// </summary>
    [Test]
    public void Get_OfASubclassProperty_ResolvesAgainstTheInstance()
    {
        LoaderBaseVM viewModel = new LoaderDerivedVM();

        Assert.That(DynamicMember.Get(viewModel, "DerivedText"), Is.EqualTo("derived"));
    }

    [Test]
    public void Get_BoxesAValueTypedProperty()
    {
        Assert.That(DynamicMember.Get(new LoaderBaseVM(), "Count"), Is.EqualTo(7));
    }

    [Test]
    public void Get_OfANonPublicGetter_ThrowsLikeTheLoader()
    {
        Assert.That(() => DynamicMember.Get(new LoaderBaseVM(), "PrivateGetterText"), Throws.Exception);
        Assert.That(() => DynamicMember.Get(new LoaderBaseVM(), "HiddenText"), Throws.Exception);
    }

    /// <summary>Verifies that property accessors that throw exceptions wrap them in <see cref="TargetInvocationException"/> to match loader reflection behavior.</summary>
    [Test]
    public void Get_OfAThrowingGetter_ThrowsTheSameWrapperTheLoaderDoes()
    {
        Assert.That(() => DynamicMember.Get(new LoaderBaseVM(), "ThrowingText"),
            Throws.InstanceOf<TargetInvocationException>().With.InnerException.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Get_WithNoHostInstalled_StillBehavesLikeTheLoader()
    {
        DynamicMember.Host = null;

        Assert.That(DynamicMember.Get(new LoaderBaseVM(), "BaseText"), Is.EqualTo("base"));
        Assert.That(DynamicMember.Get(new LoaderBaseVM(), "NoSuchProperty"), Is.Null);
        Assert.That(() => DynamicMember.Set(new LoaderBaseVM(), "WritableText", "x"), Throws.Nothing);
        Assert.That(() => DynamicMember.Execute(new LoaderBaseVM(), "NoSuchCommand", []), Throws.Nothing);
    }

    // ---------------------------------------------------------------- writes

    [Test]
    public void CombinedWrite_ReportsAMissButResolvesLaterRegistrationAndReplacement()
    {
        var host = new RecordingHost();
        DynamicMember.Host = host;
        var viewModel = new LoaderBaseVM();
        DynamicMember.Set(viewModel, "Registered", "missing");
        DynamicMember.Set(viewModel, "Registered", "missing again");

        var first = new LoaderRegisteredMemberSource("first");
        viewModel.AddProperty("Registered", Bound(first));
        DynamicMember.Set(viewModel, "Registered", "written first");
        Assert.That(viewModel.GetPropertyValue("Registered"), Is.EqualTo("written first"));

        var second = new LoaderRegisteredMemberSource("second");
        viewModel.AddProperty("Registered", Bound(second));
        DynamicMember.Set(viewModel, "Registered", "written second");
        Assert.That(viewModel.GetPropertyValue("Registered"), Is.EqualTo("written second"));
        Assert.That(first.Value, Is.EqualTo("written first"));
        Assert.That(host.Messages.Count, Is.EqualTo(1));
    }

    [Test]
    public void Set_OfAWritableProperty_Writes()
    {
        var viewModel = new LoaderBaseVM();

        DynamicMember.Set(viewModel, "WritableText", "changed");

        Assert.That(viewModel.WritableText, Is.EqualTo("changed"));
    }

    [Test]
    public void Set_OfAPropertyWithNoPublicSetter_DoesNothing()
    {
        var viewModel = new LoaderBaseVM();

        DynamicMember.Set(viewModel, "ReadOnlyText", "changed");

        Assert.That(viewModel.ReadOnlyText, Is.EqualTo("readonly"));
    }

    [Test]
    public void Set_OfANameTheInstanceDoesNotHave_DoesNothing()
    {
        Assert.That(() => DynamicMember.Set(new LoaderBaseVM(), "NoSuchProperty", "x"), Throws.Nothing);
    }

    [Test]
    public void Set_OfANullOwner_DoesNothing()
    {
        Assert.That(() => DynamicMember.Set(null, "WritableText", "x"), Throws.Nothing);
    }

    /// <summary>Verifies that setting an integer value on a single-precision floating-point property widens the argument.</summary>
    [Test]
    public void Set_OfAValueNeedingWidening_Widens()
    {
        var viewModel = new WideningVM();

        DynamicMember.Set(viewModel, "Number", 3);

        Assert.That(viewModel.Number, Is.EqualTo(3f));
    }

    /// <summary>Verifies that assigning null to a value-typed property applies the default value of the target type.</summary>
    [Test]
    public void Set_OfNull_OnAValueTypedProperty_AssignsTheDefault()
    {
        var viewModel = new WideningVM { Number = 5f };

        DynamicMember.Set(viewModel, "Number", null);

        Assert.That(viewModel.Number, Is.Zero);
    }

    [Test]
    public void Set_OfAThrowingSetter_ThrowsTheSameWrapperTheLoaderDoes()
    {
        Assert.That(() => DynamicMember.Set(new LoaderBaseVM(), "ThrowingSetterText", "x"),
            Throws.InstanceOf<TargetInvocationException>().With.InnerException.InstanceOf<InvalidOperationException>());
    }

    // ---------------------------------------------------------------- registered members

    [Test]
    public void Get_OfARegisteredMember_ReadsItsOwnReceiver()
    {
        var first = new LoaderBaseVM();
        var second = new LoaderBaseVM();
        first.AddProperty("Registered", Bound(new LoaderRegisteredMemberSource("first")));
        second.AddProperty("Registered", Bound(new LoaderRegisteredMemberSource("second")));

        Assert.That(DynamicMember.Get(first, "Registered"), Is.EqualTo("first"));
        Assert.That(DynamicMember.Get(second, "Registered"), Is.EqualTo("second"));
        Assert.That(DynamicMember.Get(first, "Registered"), Is.EqualTo("first"),
            "reading one instance never disturbs what another answers");
    }

    /// <summary>
    /// Verifies that reading dynamically registered properties across multiple instances maintains instance isolation.
    /// </summary>
    [Test]
    public void Get_OfARegisteredMember_NeverServesOneInstancesDataToAnother()
    {
        var viewModels = new LoaderBaseVM[16];
        for (var i = 0; i < viewModels.Length; i++)
        {
            viewModels[i] = new LoaderBaseVM();
            viewModels[i].AddProperty("Registered", Bound(new LoaderRegisteredMemberSource("value" + i)));
        }

        for (var i = 0; i < viewModels.Length; i++)
            Assert.That(DynamicMember.Get(viewModels[i], "Registered"), Is.EqualTo("value" + i));
    }

    [Test]
    public void Set_OfARegisteredMember_WritesToItsOwnReceiver()
    {
        var viewModel = new LoaderBaseVM();
        var source = new LoaderRegisteredMemberSource("before");
        viewModel.AddProperty("Registered", Bound(source));

        DynamicMember.Set(viewModel, "Registered", "after");

        Assert.That(source.Value, Is.EqualTo("after"));
    }

    /// <summary>Verifies that an initial failed lookup does not cache the miss when the property is subsequently registered.</summary>
    [Test]
    public void AMissIsNotRemembered()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(DynamicMember.Get(viewModel, "Registered"), Is.Null);

        viewModel.AddProperty("Registered", Bound(new LoaderRegisteredMemberSource("late")));

        Assert.That(DynamicMember.Get(viewModel, "Registered"), Is.EqualTo("late"));
    }

    /// <summary>Verifies that dynamically registering a property overrides a previously resolved declared property.</summary>
    [Test]
    public void ASelectionIsNotRememberedAcrossAReplacement()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(DynamicMember.Get(viewModel, "BaseText"), Is.EqualTo("base"));

        viewModel.AddProperty("BaseText", Bound(new LoaderRegisteredMemberSource("replaced")));

        Assert.That(DynamicMember.Get(viewModel, "BaseText"), Is.EqualTo("replaced"));
    }

    /// <summary>Verifies that unrecognised member property representations delegate resolution to the loader fallback.</summary>
    [Test]
    public void AnUnrecognisedMemberRepresentation_UsesTheLoader()
    {
        var viewModel = new LoaderBaseVM();
        viewModel.AddProperty("Opaque", new OpaquePropertyInfo("opaque"));

        Assert.That(DynamicMember.Get(viewModel, "Opaque"), Is.EqualTo("opaque"));
    }

    // ---------------------------------------------------------------- children

    [Test]
    public void GetChild_OfAViewModelProperty_IsTheViewModel()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(DynamicMember.GetChild(viewModel, "Child"), Is.SameAs(viewModel.Child));
    }

    [Test]
    public void GetChild_OfAListProperty_IsTheList()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(DynamicMember.GetChild(viewModel, "Children"), Is.SameAs(viewModel.Children));
    }

    [Test]
    public void GetChild_OfSomethingThatIsNeither_IsNull()
    {
        Assert.That(DynamicMember.GetChild(new LoaderBaseVM(), "Scalar"), Is.Null);
    }

    [Test]
    public void GetChild_OfANullChild_IsNull()
    {
        Assert.That(DynamicMember.GetChild(new LoaderBaseVM { Child = null }, "Child"), Is.Null);
    }

    [Test]
    public void GetChild_OfANameTheInstanceDoesNotHave_IsNull()
    {
        Assert.That(DynamicMember.GetChild(new LoaderBaseVM(), "NoSuchChild"), Is.Null);
    }

    // ---------------------------------------------------------------- commands

    [Test]
    public void Execute_RunsTheCommand()
    {
        var viewModel = new LoaderBaseVM();

        DynamicMember.Execute(viewModel, "ExecuteNoArguments", []);

        Assert.That(viewModel.CommandCalls, Is.EqualTo(1));
    }

    [Test]
    public void Execute_FindsASubclassCommandThroughABaseTypedReference()
    {
        LoaderBaseVM viewModel = new LoaderDerivedVM();

        DynamicMember.Execute(viewModel, "ExecuteDerived", []);

        Assert.That(((LoaderDerivedVM) viewModel).DerivedCommandCalls, Is.EqualTo(1));
    }

    [Test]
    public void Execute_FindsAPrivateCommand()
    {
        var viewModel = new LoaderBaseVM();

        DynamicMember.Execute(viewModel, "ExecutePrivate", []);

        Assert.That(viewModel.CommandCalls, Is.EqualTo(1));
    }

    [Test]
    public void Execute_OfANameTheInstanceDoesNotHave_DoesNothing()
    {
        var viewModel = new LoaderBaseVM();

        Assert.That(() => DynamicMember.Execute(viewModel, "NoSuchCommand", []), Throws.Nothing);
        Assert.That(viewModel.CommandCalls, Is.Zero);
    }

    [Test]
    public void Execute_OfANullOwner_DoesNothing()
    {
        Assert.That(() => DynamicMember.Execute(null, "ExecuteNoArguments", []), Throws.Nothing);
    }

    [Test]
    public void Execute_KeepsTheLoadersStringConversion()
    {
        var viewModel = new LoaderBaseVM();

        DynamicMember.Execute(viewModel, "ExecuteWithInt", ["42"]);

        Assert.That(viewModel.LastIntArgument, Is.EqualTo(42));
    }

    [Test]
    public void Execute_KeepsTheLoadersCompatibilityCheck()
    {
        var viewModel = new LoaderBaseVM();

        DynamicMember.Execute(viewModel, "ExecuteWithString", [17]);

        Assert.That(viewModel.CommandCalls, Is.Zero);
    }

    [Test]
    public void Execute_KeepsARegisteredMethodsPrecedence()
    {
        var viewModel = new LoaderBaseVM();
        viewModel.AddMethod("ExecuteNoArguments", BoundMethod(new LoaderRegisteredMemberSource("value")));

        DynamicMember.Execute(viewModel, "ExecuteNoArguments", ["!"]);

        Assert.That(viewModel.CommandCalls, Is.Zero);
    }

    // ---------------------------------------------------------------- diagnostics

    /// <summary>
    /// Verifies that unresolved member lookups report diagnostic warnings exactly once per target type, member name, and operation type.
    /// </summary>
    [Test]
    public void AMissIsReportedOncePerTypeNameAndOperation()
    {
        var host = new RecordingHost();
        DynamicMember.Host = host;

        for (var i = 0; i < 5; i++)
        {
            DynamicMember.Get(new LoaderBaseVM(), "NoSuchProperty");
            DynamicMember.Set(new LoaderBaseVM(), "NoSuchProperty", "x");
            DynamicMember.Execute(new LoaderBaseVM(), "NoSuchCommand", []);
        }

        Assert.That(host.Messages.Count, Is.EqualTo(3), "once for the read, once for the write, once for the command");
        Assert.That(host.Messages, Has.All.Contains(nameof(LoaderBaseVM)));
    }

    /// <summary>Verifies that missing member lookups report separate diagnostic warnings for distinct runtime types.</summary>
    [Test]
    public void AMissIsReportedSeparatelyForEachRuntimeType()
    {
        var host = new RecordingHost();
        DynamicMember.Host = host;

        DynamicMember.Get(new LoaderBaseVM(), "NoSuchProperty");
        DynamicMember.Get(new LoaderDerivedVM(), "NoSuchProperty");

        Assert.That(host.Messages.Count, Is.EqualTo(2));
    }

    /// <summary>
    /// Verifies that logging a diagnostic warning for a missing member does not prevent subsequent successful resolution after dynamic registration.
    /// </summary>
    [Test]
    public void ReportingAMissDoesNotFreezeIt()
    {
        var host = new RecordingHost();
        DynamicMember.Host = host;
        var viewModel = new LoaderBaseVM();

        DynamicMember.Get(viewModel, "Registered");
        viewModel.AddProperty("Registered", Bound(new LoaderRegisteredMemberSource("late")));

        Assert.That(DynamicMember.Get(viewModel, "Registered"), Is.EqualTo("late"));
        Assert.That(host.Messages.Count, Is.EqualTo(1));
    }

    private sealed class RecordingHost : IDynamicMemberHost
    {
        private readonly DynamicMemberHost _inner = new();

        public List<string> Messages { get; } = [];

        public bool HasProperty(ViewModel target, string name) => _inner.HasProperty(target, name);

        public bool HasMethod(ViewModel target, string name) => _inner.HasMethod(target, name);

        public bool TrySetProperty(ViewModel target, string name, object? value) => _inner.TrySetProperty(target, name, value);

        public void Report(string message) => Messages.Add(message);
    }

    private static PropertyInfo Bound(LoaderRegisteredMemberSource source) =>
        new Bannerlord.BUTR.Shared.Utils.WrappedPropertyInfo(
            typeof(LoaderRegisteredMemberSource).GetProperty(nameof(LoaderRegisteredMemberSource.Value))!, source);

    private static MethodInfo BoundMethod(LoaderRegisteredMemberSource source) =>
        new Bannerlord.BUTR.Shared.Utils.WrappedMethodInfo(
            typeof(LoaderRegisteredMemberSource).GetMethod(nameof(LoaderRegisteredMemberSource.Echo))!, source);
}
