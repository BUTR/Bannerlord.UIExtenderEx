namespace Bannerlord.UIExtenderEx.ViewModels;

/// <summary>
/// Interface for <see cref="TaleWorlds.Library.ViewModel"/> mixins.<br/>
/// Should not be used directly, <see cref="BaseViewModelMixin{TViewModel}"/> should be used as base class.
/// </summary>
public interface IViewModelMixin
{
    /// <summary>
    /// Called when the original ViewModel is refreshed. The method name is dynamic, you need to set
    /// <see cref="Bannerlord.UIExtenderEx.Attributes.ViewModelMixinAttribute.RefreshMethodName"/> for the method to be called.<br/>
    /// Defaults to an empty method.
    /// </summary>
    void OnRefresh();

    /// <summary>
    /// Called when the original's <see cref="TaleWorlds.Library.ViewModel.OnFinalize"/> is called.<br/>
    /// Defaults to an empty method.
    /// </summary>
    void OnFinalize();
}