using Bannerlord.UIExtenderEx.Attributes;

using System;
using System.Globalization;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Defines test ViewModel member shapes used to verify binding contracts in <see cref="LoaderSemanticsTests"/>.
/// </summary>
public class LoaderBaseVM : ViewModel
{
    [DataSourceProperty]
    public string BaseText => "base";

    /// <summary>Represents a property containing null, testing query parity with absent members in <c>GetPropertyValue</c>.</summary>
    [DataSourceProperty]
    public string? NullText => null;

    [DataSourceProperty]
    public bool BaseFlag => true;

    /// <summary>Represents a property with a public getter and private setter, permitting reads and silently ignoring writes.</summary>
    [DataSourceProperty]
    public string ReadOnlyText { get; private set; } = "readonly";

    [DataSourceProperty]
    public string WritableText { get; set; } = "writable";

    /// <summary>Represents a non-public property registered in the binding table that throws on invocation.</summary>
    protected string HiddenText => "hidden";

    /// <summary>Represents a public property with a private getter whose public accessor method returns null.</summary>
    public string PrivateGetterText { private get; set; } = "private getter";

    [DataSourceProperty]
    public string ThrowingText => throw new InvalidOperationException("getter blew up");

    public string ThrowingSetterText
    {
        get => "throwing setter";
        set => throw new InvalidOperationException("setter blew up");
    }

    [DataSourceProperty]
    public int Count => 7;

    [DataSourceProperty]
    public LoaderChildVM? Child { get; set; } = new();

    [DataSourceProperty]
    public MBBindingList<LoaderChildVM> Children { get; } = [];

    /// <summary>Represents a scalar property that cannot serve as an intermediate path segment.</summary>
    [DataSourceProperty]
    public string Scalar => "scalar";

    /// <summary>Represents a property accessed exclusively via <see cref="OpaquePropertyInfo"/> wrappers.</summary>
    public string OpaqueText => "opaque";

    public int CommandCalls { get; private set; }
    public string? LastStringArgument { get; private set; }
    public int LastIntArgument { get; private set; }
    public ViewModel? LastViewModelArgument { get; private set; }

    public void ExecuteNoArguments() => CommandCalls++;

    public void ExecuteWithString(string value)
    {
        CommandCalls++;
        LastStringArgument = value;
    }

    public void ExecuteWithInt(int value)
    {
        CommandCalls++;
        LastIntArgument = value;
    }

    public void ExecuteWithViewModel(ViewModel value)
    {
        CommandCalls++;
        LastViewModelArgument = value;
    }

    private void ExecutePrivate() => CommandCalls++;

    public void ExecuteThrowing() => throw new InvalidOperationException("command blew up");

    /// <summary>Represents a command method declared on the base class and invoked on derived instances.</summary>
    public void ExecuteInherited() => CommandCalls++;
}

public class LoaderDerivedVM : LoaderBaseVM
{
    [DataSourceProperty]
    public string DerivedText => "derived";

    [DataSourceProperty]
    public bool DerivedFlag => true;

    public int DerivedCommandCalls { get; private set; }

    public void ExecuteDerived() => DerivedCommandCalls++;
}

public class LoaderOtherDerivedVM : LoaderBaseVM
{
    [DataSourceProperty]
    public string DerivedText => "other derived";
}

/// <summary>
/// Represents a derived ViewModel that raises notifications carrying payloads that diverge from getter return values.
/// </summary>
public class NotifyingDerivedVM : LoaderBaseVM
{
    private string _derivedText = "first";

    [DataSourceProperty]
    public string DerivedText => _derivedText;

    [DataSourceProperty]
    public bool DerivedFlag => true;

    /// <summary>Represents a writable property declared solely on the derived class to test dynamic writeback discovery.</summary>
    [DataSourceProperty]
    public bool WritableFlag { get; set; }

    public void SetDerivedText(string value)
    {
        _derivedText = value;
        OnPropertyChanged(nameof(DerivedText));
    }

    public void AnnounceDerivedTextAs(string value) => OnPropertyChangedWithValue(value, nameof(DerivedText));

    public void AnnounceDerivedFlagAs(bool value) => OnPropertyChangedWithValue(value, nameof(DerivedFlag));

    public void AnnounceBaseTextAs(string value) => OnPropertyChangedWithValue(value, nameof(BaseText));
}

public class LoaderChildVM : ViewModel
{
    [DataSourceProperty]
    public string ChildText => "child";
}

/// <summary>
/// Represents a data source whose declared type provides a base child scope while derived runtime classes expose additional scopes.
/// </summary>
public class ScopeRootVM : ViewModel
{
    [DataSourceProperty]
    public ScopeLeafVM? Known { get; set; } = new("known");
}

public class ScopeDerivedVM : ScopeRootVM
{
    private ScopeBranchVM? _extra = new("first");

    [DataSourceProperty]
    public ScopeBranchVM? Extra => _extra;

    public void SetExtra(ScopeBranchVM? value)
    {
        _extra = value;
        OnPropertyChangedWithValue(value, nameof(Extra));
    }
}

public class ScopeOtherDerivedVM : ScopeRootVM
{
    [DataSourceProperty]
    public ScopeBranchVM Extra { get; } = new("other");
}

/// <summary>Represents an intermediate scope level facilitating multi-hop path traversal.</summary>
public class ScopeBranchVM : ViewModel
{
    public ScopeBranchVM(string text) => BranchText = text;

    [DataSourceProperty]
    public string BranchText { get; }

    [DataSourceProperty]
    public ScopeLeafVM Deeper { get; } = new("deep");
}

public class ScopeLeafVM : ViewModel
{
    public ScopeLeafVM(string text) => LeafText = text;

    [DataSourceProperty]
    public string LeafText { get; }
}

/// <summary>
/// Represents a child scope exposed via an internal getter to verify accessor enforcement parity.
/// </summary>
public class HiddenScopeVM : ViewModel
{
    [DataSourceProperty]
    internal ScopeLeafVM HiddenChild { get; } = new("hidden");
}

/// <summary>
/// Represents a root data source lacking list properties, designed for derived types providing runtime lists without compile-time element types.
/// </summary>
public class ListRootVM : ViewModel
{
    [DataSourceProperty]
    public string RootText => "root";
}

public class ListDerivedVM : ListRootVM
{
    private MBBindingList<ListItemVM> _items = [];

    [DataSourceProperty]
    public MBBindingList<ListItemVM> Items => _items;

    public void Replace(MBBindingList<ListItemVM> items)
    {
        _items = items;
        OnPropertyChangedWithValue(items, nameof(Items));
    }
}

/// <summary>Represents a list property declared via non-generic interface <see cref="IMBBindingList"/>.</summary>
public class InterfaceListVM : ListRootVM
{
    [DataSourceProperty]
    public IMBBindingList Items { get; } = new MBBindingList<ListItemVM>();
}

/// <summary>Represents a non-generic class inheriting from a generic <see cref="MBBindingList{T}"/>.</summary>
public class NonGenericItemList : MBBindingList<ListItemVM>;

public class NonGenericListVM : ListRootVM
{
    [DataSourceProperty]
    public NonGenericItemList Items { get; } = [];
}

public class ListItemVM : ViewModel
{
    public ListItemVM(string text) => ItemText = text;

    [DataSourceProperty]
    public string ItemText { get; }
}

/// <summary>
/// Represents a command host whose base class provides a typed command while derived classes supply additional commands invoked dynamically.
/// </summary>
public class CommandRootVM : ViewModel
{
    public int BaseCalls { get; private set; }

    [DataSourceProperty]
    public MBBindingList<ListItemVM> Items { get; } = [];

    public void ExecuteBaseCommand() => BaseCalls++;
}

public class CommandDerivedVM : CommandRootVM
{
    public int Calls { get; private set; }
    public string? LastString { get; private set; }
    public int LastInt { get; private set; }
    public object? LastArgument { get; private set; }

    public void ExecuteSubclassCommand() => Calls++;

    private void ExecutePrivateCommand() => Calls++;

    public void ExecuteWithString(string value)
    {
        Calls++;
        LastString = value;
    }

    public void ExecuteWithInt(int value)
    {
        Calls++;
        LastInt = value;
    }

    public void ExecuteWithSource(ViewModel value)
    {
        Calls++;
        LastArgument = value;
    }

    public void ExecuteWithList(MBBindingList<ListItemVM> value)
    {
        Calls++;
        LastArgument = value;
    }
}

/// <summary>Simulates dynamically registered mixin members attached to individual instances.</summary>
public class LoaderRegisteredMemberSource
{
    public LoaderRegisteredMemberSource(string value) => Value = value;

    public string Value { get; set; }

    public string Echo(string suffix) => Value + suffix;
}

public class WideningVM : ViewModel
{
    /// <summary>Accepts integer arguments widened into single-precision floating-point values via reflection binding.</summary>
    [DataSourceProperty]
    public float Number { get; set; }
}

/// <summary>
/// Represents an unrecognised <see cref="PropertyInfo"/> wrapper that delegates property resolution to the loader reflection fallback.
/// </summary>
public sealed class OpaquePropertyInfo : PropertyInfo
{
    private readonly PropertyInfo _wrapped;

    public OpaquePropertyInfo(string name) =>
        _wrapped = typeof(LoaderBaseVM).GetProperty(nameof(LoaderBaseVM.OpaqueText))!;

    public override PropertyAttributes Attributes => _wrapped.Attributes;
    public override bool CanRead => _wrapped.CanRead;
    public override bool CanWrite => _wrapped.CanWrite;
    public override Type? DeclaringType => _wrapped.DeclaringType;
    public override string Name => _wrapped.Name;
    public override Type PropertyType => _wrapped.PropertyType;
    public override Type? ReflectedType => _wrapped.ReflectedType;

    public override MethodInfo[] GetAccessors(bool nonPublic) => _wrapped.GetAccessors(nonPublic);
    public override object[] GetCustomAttributes(bool inherit) => _wrapped.GetCustomAttributes(inherit);
    public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _wrapped.GetCustomAttributes(attributeType, inherit);
    public override MethodInfo? GetGetMethod(bool nonPublic) => _wrapped.GetGetMethod(nonPublic);
    public override ParameterInfo[] GetIndexParameters() => _wrapped.GetIndexParameters();
    public override MethodInfo? GetSetMethod(bool nonPublic) => _wrapped.GetSetMethod(nonPublic);
    public override object? GetValue(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture) =>
        _wrapped.GetValue(obj, invokeAttr, binder, index, culture);
    public override bool IsDefined(Type attributeType, bool inherit) => _wrapped.IsDefined(attributeType, inherit);
    public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture) =>
        _wrapped.SetValue(obj, value, invokeAttr, binder, index, culture);
}
