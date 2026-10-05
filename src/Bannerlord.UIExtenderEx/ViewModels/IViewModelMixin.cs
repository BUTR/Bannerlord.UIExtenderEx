namespace Bannerlord.UIExtenderEx.ViewModels;

/// <summary>
/// Defines the contract for ViewModel mixin extensions.
/// <para>
/// Custom mixins should inherit from <see cref="BaseViewModelMixin{TViewModel}"/> instead of implementing this interface directly.
/// </para>
/// </summary>
public interface IViewModelMixin
{
    /// <summary>
    /// Invoked when the host ViewModel executes its designated refresh method (configured via <see cref="Attributes.ViewModelMixinAttribute.RefreshMethodName"/>).
    /// </summary>
    void OnRefresh();

    /// <summary>
    /// Invoked when the host ViewModel executes <see cref="TaleWorlds.Library.ViewModel.OnFinalize"/>, providing an opportunity
    /// for cleanup and resource disposal.
    /// </summary>
    void OnFinalize();
}