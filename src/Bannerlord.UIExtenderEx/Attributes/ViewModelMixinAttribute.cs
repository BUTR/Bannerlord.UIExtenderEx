using Bannerlord.UIExtenderEx.ViewModels;

using System;

namespace Bannerlord.UIExtenderEx.Attributes;

/// <summary>
/// Marks a class as a ViewModel mixin extending a target <see cref="TaleWorlds.Library.ViewModel"/>.
/// <para>
/// Mixin classes must inherit from <see cref="BaseViewModelMixin{TViewModel}"/>. When attached, the mixin exposes
/// its decorated properties and methods to the host ViewModel's Gauntlet data-binding context.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ViewModelMixinAttribute : BaseUIExtenderAttribute
{
    /// <summary>
    /// Gets the name of the host ViewModel method (such as <c>RefreshValues</c>) whose execution triggers
    /// <see cref="BaseViewModelMixin{TViewModel}.OnRefresh"/>.
    /// </summary>
    public string? RefreshMethodName { get; }

    /// <summary>
    /// Gets a value indicating whether this mixin attaches to all subtypes derived from the target ViewModel,
    /// in addition to the target type itself.
    /// </summary>
    public bool HandleDerived { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ViewModelMixinAttribute"/> with default configuration.
    /// </summary>
    public ViewModelMixinAttribute() { }

    /// <summary>
    /// Initializes a new instance of <see cref="ViewModelMixinAttribute"/> with a designated refresh method name.
    /// </summary>
    /// <param name="refreshMethodName">The name of the host ViewModel method that triggers <see cref="BaseViewModelMixin{TViewModel}.OnRefresh"/>.</param>
    public ViewModelMixinAttribute(string refreshMethodName)
    {
        RefreshMethodName = refreshMethodName;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ViewModelMixinAttribute"/> specifying derived type handling.
    /// </summary>
    /// <param name="handleDerived"><see langword="true"/> to attach to derived types; otherwise, <see langword="false"/>.</param>
    public ViewModelMixinAttribute(bool handleDerived)
    {
        HandleDerived = handleDerived;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ViewModelMixinAttribute"/> with both refresh method and derived type configuration.
    /// </summary>
    /// <param name="refreshMethodName">The name of the host ViewModel method that triggers <see cref="BaseViewModelMixin{TViewModel}.OnRefresh"/>.</param>
    /// <param name="handleDerived"><see langword="true"/> to attach to derived types; otherwise, <see langword="false"/>.</param>
    public ViewModelMixinAttribute(string? refreshMethodName = null, bool handleDerived = false)
    {
        RefreshMethodName = refreshMethodName;
        HandleDerived = handleDerived;
    }
}