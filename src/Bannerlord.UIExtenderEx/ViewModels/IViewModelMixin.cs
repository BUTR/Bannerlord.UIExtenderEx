namespace Bannerlord.UIExtenderEx.ViewModels
{
    /// <summary>
    /// Interface for ViewModel mixins
    /// Should not be used directly, ViewModelMixin should be used as base class
    /// </summary>
    public interface IViewModelMixin
    {
        /// <summary>
        /// Called when ViewModel is refreshed (specifics are based on ViewModel patch)
        /// </summary>
        void OnRefresh();

        /// <summary>
        /// Called when ViewModel's `OnFinalized` called (supported on models game actually call `OnFinalized`)
        /// </summary>
        void OnFinalize();
    }
}