using Bannerlord.UIExtenderEx.Attributes;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// A three-level property chain, the shape of a dotted attribute path such as <c>Text.Left.Margin</c>.
/// Widgets reach two levels in shipped prefabs (<c>Brush.Color</c>); nothing stops patched XML going deeper.
/// </summary>
public class PropertyPathRoot
{
    public PropertyPathBranch Text { get; } = new();

    /// <summary>Same chain under a longer name, which moves where the second segment is cut from.</summary>
    public PropertyPathBranch Container { get; } = new();

    public int Width { get; set; }

    internal PropertyPathBranch Hidden { get; } = new();
}

public class PropertyPathBranch
{
    public PropertyPathLeaf Left { get; } = new();
}

public class PropertyPathLeaf
{
    public int Margin { get; set; }
}

/// <summary>
/// Non-public members on a ViewModel. <see cref="ViewModel"/> collects these into its binding table, which makes them
/// look bindable, but the loader reaches a property through <c>GetGetMethod()</c> and <c>GetSetMethod()</c> and so can
/// only ever use the public ones: reading a non-public property throws inside the loader, writing one does nothing.
/// These types exist to hold the generator to the same line.
/// </summary>
public class NonPublicMemberVM : ViewModel
{
    [DataSourceProperty]
    public string PublicText { get; set; } = "public";

    [DataSourceProperty]
    public string ReadOnlyText => "read only";

    [DataSourceProperty]
    internal string InternalText { get; set; } = "internal";

    [DataSourceProperty]
    public string InternalSetterText { get; internal set; } = "internal setter";

    /// <summary>A non-public getter, so the generator has to walk into it without asking for the public accessor.</summary>
    [DataSourceProperty]
    internal NonPublicChildVM InternalChild { get; } = new();
}

public class NonPublicChildVM : ViewModel
{
    [DataSourceProperty]
    public string ChildText { get; set; } = "child";
}

/// <summary>Plain ViewModel for the prefab-structure tests, which are about widgets rather than members.</summary>
public class StructureVM : ViewModel
{
    [DataSourceProperty]
    public string Title => "title";

    [DataSourceProperty]
    public NonPublicChildVM ChildA { get; } = new();

    /// <summary>A different type with the same bound member, so the same part prefab fits under either child.</summary>
    [DataSourceProperty]
    public OtherChildVM ChildB { get; } = new();
}

public class OtherChildVM : ViewModel
{
    [DataSourceProperty]
    public string ChildText { get; set; } = "other";
}

/// <summary>Stands for a mod widget class that only exists because it was registered at runtime.</summary>
public class LookupWidget : Widget
{
    public LookupWidget(UIContext context) : base(context) { }
}

/// <summary>Commands with and without an argument, for the <c>CommandParameter.X</c> tests.</summary>
public class CommandVM : ViewModel
{
    [DataSourceProperty]
    public string Title => "title";

    public void ExecuteNoArgs() { }

    public void ExecuteWithTag(string tag) { }
}

/// <summary>ViewModel and widget property types that do not line up, for the conversion tests.</summary>
public class ConversionVM : ViewModel
{
    /// <summary>Bound to a Sprite widget property: the loader converts a string, and clears the property on null.</summary>
    [DataSourceProperty]
    public string SpriteName { get; set; } = "sprite";

    /// <summary>Bound to a float widget property: no conversion in the loader, but reflection widens on its way in.</summary>
    [DataSourceProperty]
    public int Width { get; set; }

    /// <summary>Bound to a string widget property from an int: neither converts, so nothing should be emitted.</summary>
    [DataSourceProperty]
    public int NotConvertible { get; set; }
}

/// <summary>Commands whose arguments the loader swaps for the data source behind the widget that was passed.</summary>
public class CommandArgumentVM : ViewModel
{
    [DataSourceProperty]
    public string Title => "title";

    public void ExecuteWithViewModel(NonPublicChildVM argument) { }

    public void ExecuteWithObject(object argument) { }

    public void ExecuteWithString(string argument) { }
}

/// <summary>A bound list, for the item-template tests.</summary>
public class ListVM : ViewModel
{
    [DataSourceProperty]
    public MBBindingList<NonPublicChildVM> Items { get; } = [];
}
