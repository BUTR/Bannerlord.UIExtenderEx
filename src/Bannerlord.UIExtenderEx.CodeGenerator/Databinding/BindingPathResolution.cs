using System;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Encapsulates the resolved code-generation strategy and emitted type information for a databinding path.
/// <para>
/// Distinguishes between collection bindings (<see cref="IMBBindingList"/>) and view model object references (<see cref="ViewModel"/>),
/// enabling emitters to generate strongly typed field declarations, list subscriptions, or dynamic by-name fallbacks consistently.
/// </para>
/// </summary>
internal sealed class BindingPathResolution
{
    private BindingPathResolution(bool isList, Type? elementType, string emittedTypeName)
    {
        IsList = isList;
        ElementType = elementType;
        EmittedTypeName = emittedTypeName;
    }

    /// <summary>Gets a value indicating whether this binding path resolves to a binding list collection.</summary>
    public bool IsList { get; }

    /// <summary>Gets the collection item element type if known; otherwise, <see langword="null"/>.</summary>
    public Type? ElementType { get; }

    /// <summary>Gets the fully-qualified C# type name emitted for variable declarations and casts.</summary>
    public string EmittedTypeName { get; }

    /// <summary>Gets a value indicating whether the binding list has a resolvable element type.</summary>
    public bool HasElementType => ElementType is not null;

    public static BindingPathResolution ForViewModel(Type declaredType) =>
        new(isList: false, elementType: null, ViewModelMemberResolution.GetCodeTypeName(declaredType)!);

    public static BindingPathResolution ForList(Type elementType) =>
        new(isList: true, elementType, $"global::TaleWorlds.Library.MBBindingList<{ViewModelMemberResolution.GetCodeTypeName(elementType)}>");

    /// <summary>
    /// Represents an unresolved view model path falling back to dynamic by-name property resolution on <see cref="ViewModel"/>.
    /// </summary>
    public static readonly BindingPathResolution UnresolvedViewModel = new(isList: false, elementType: null, "global::TaleWorlds.Library.ViewModel");

    /// <summary>
    /// Gets a value indicating whether the data source must also be tracked as a raw <see cref="object"/> field.
    /// </summary>
    public bool HoldsObject => ReferenceEquals(this, UnresolvedViewModel);

    /// <summary>
    /// Represents an unresolved list path binding against the non-generic <see cref="IMBBindingList"/> interface.
    /// </summary>
    public static readonly BindingPathResolution UnresolvedList = new(isList: true, elementType: null, "global::TaleWorlds.Library.IMBBindingList");
}