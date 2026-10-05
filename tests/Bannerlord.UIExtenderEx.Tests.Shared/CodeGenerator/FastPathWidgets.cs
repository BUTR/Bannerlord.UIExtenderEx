using System;
using System.Reflection;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// Defines representative widget property patterns evaluated by the typed fast path code generator on a single widget class resolvable by test prefabs.
/// The widget factory locates the widget type directly within the test mod assembly.
/// <para>
/// Each property models a code generation rule: public setters with resolvable types utilize direct typed assignments, while complex or unresolvable properties fall back to <c>WidgetExtensions.SetWidgetAttribute</c>.
/// </para>
/// </summary>
public class FastPathWidget : Widget
{
    public FastPathWidget(UIContext context) : base(context) { }

    private bool _flag;

    /// <summary>
    /// Represents a direct settable single-segment property targeted by the fast path. Raises property change notifications matching TaleWorlds widget behavior to enable two-way write-back bindings.
    /// </summary>
    public bool Flag
    {
        get => _flag;
        set
        {
            if (_flag == value)
                return;
            _flag = value;
            OnPropertyChanged(value);
        }
    }

    public int Number { get; set; }

    /// <summary>Widens an integer binding value into a float, which the fast path emits as an explicit cast.</summary>
    public float Ratio { get; set; }

    public double Precise { get; set; }

    public uint Unsigned { get; set; }

    public Color Tint { get; set; }

    public Vec2 Offset { get; set; }

    /// <summary>Represents a reference type property where matching non-null values assign directly and null values invoke the fallback reflection path.</summary>
    public string? Label { get; set; }

    /// <summary>Requires conversion from a string through <c>ConvertObject</c>, which bypasses the typed fast path.</summary>
    public Sprite? Picture { get; set; }

    public FastPathMode Mode { get; set; }

    /// <summary>Provides a read-only property where <c>GetSetMethod()</c> returns null, verifying that loader calls throw an exception rather than failing silently.</summary>
    public bool ReadOnlyFlag => true;

    /// <summary>Provides a public property with an internal setter where <c>GetSetMethod()</c> returns null, verifying fallback exception handling.</summary>
    public bool InternalSetterFlag { get; internal set; }

    private bool? _maybeFlag;

    /// <summary>
    /// Provides a nullable value type property whose type check cannot be emitted directly by the generator. Raises property change notifications via the object overload.
    /// </summary>
    public bool? MaybeFlag
    {
        get => _maybeFlag;
        set
        {
            if (_maybeFlag == value)
                return;
            _maybeFlag = value;
            OnPropertyChanged((object?) value);
        }
    }

    /// <summary>Represents the root segment of a multi-segment dotted property path referencing a shared nested object.</summary>
    public FastPathBranch Branch { get; } = new();

    /// <summary>Represents a dotted property path traversing a value type struct, verifying boxed mutation behavior.</summary>
    public FastPathBox Box { get; set; }

    /// <summary>Represents a dotted property path traversing a null reference, verifying null-safe short-circuiting.</summary>
    public FastPathBranch? MissingBranch => null;

    /// <summary>
    /// Represents a property path whose root segment is typed as an interface, requiring dynamic dispatch rather than compile-time type resolution.
    /// </summary>
    public IFastPathSurface Surface { get; } = new FastPathSurface();

    /// <summary>Represents an indexer property that reflection resolves as <c>Item</c>, verifying that single-argument assignments fail gracefully.</summary>
    public bool this[int index]
    {
        get => Flag;
        set => Flag = value;
    }

    public int FlagWrites { get; private set; }

    private bool _countedFlag;

    /// <summary>Tracks assignment counts to detect duplicate property writes across code generation paths.</summary>
    public bool CountedFlag
    {
        get => _countedFlag;
        set
        {
            _countedFlag = value;
            FlagWrites++;
        }
    }

    /// <summary>
    /// Throws an exception when assigned to true, allowing movie instantiation and attachment prior to triggering setter failures.
    /// </summary>
    public bool ThrowingFlag
    {
        get => false;
        set
        {
            if (value)
                throw new InvalidOperationException("widget setter blew up");
        }
    }

    /// <summary>
    /// Throws <see cref="TargetInvocationException"/> directly from the setter to verify parity between direct invocation and reflection wrapper semantics.
    /// </summary>
    public bool DoublyWrappedFlag
    {
        get => false;
        set
        {
            if (value)
                throw new TargetInvocationException("thrown by the setter itself", new InvalidOperationException("inner"));
        }
    }
}

public enum FastPathMode
{
    First,
    Second,
}

public class FastPathBranch
{
    public int Margin { get; set; }
}

public struct FastPathBox
{
    public int Inset { get; set; }
}

public interface IFastPathSurface
{
    int Depth { get; set; }
}

public class FastPathSurface : IFastPathSurface
{
    public int Depth { get; set; }
}