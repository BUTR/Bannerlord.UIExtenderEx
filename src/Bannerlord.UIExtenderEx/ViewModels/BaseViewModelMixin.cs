using Bannerlord.UIExtenderEx.Extensions;

using System;

namespace Bannerlord.UIExtenderEx.ViewModels
{
    /// <summary>
    /// Base class for ViewModelMixin.
    /// Generic parameter T will be used to determine which VM to extend.
    /// You can use protected _vm to access fields of the original view model.
    /// </summary>
    /// <typeparam name="TViewModel">child of ViewModel this mixin is extending</typeparam>
    public abstract class BaseViewModelMixin<TViewModel> : IViewModelMixin where TViewModel : TaleWorlds.Library.ViewModel
    {
        /// <summary>
        /// ViewModel instance this mixin is attached to
        /// </summary>
        private readonly WeakReference<TViewModel> _vm;
        protected TViewModel? ViewModel => _vm.TryGetTarget(out var vm) ? vm : null;

        protected BaseViewModelMixin(TViewModel vm)
        {
            _vm = new WeakReference<TViewModel>(vm);
        }

        /// <summary>
        /// Called when ViewModel is refreshed (specifics are based on ViewModel patch).
        /// Defaults to empty method.
        /// </summary>
        public virtual void OnRefresh() { }

        /// <summary>
        /// Called when ViewModel's `OnFinalized` called (supported on models game actually call `OnFinalized`).
        /// Defaults to empty method.
        /// </summary>
        public virtual void OnFinalize() { }

        /// <summary>
        /// Helper method to get private value from attached view model instance
        /// </summary>
        /// <param name="name">name of the field</param>
        /// <typeparam name="TValue">type</typeparam>
        /// <returns></returns>
        protected TValue? GetPrivate<TValue>(string name) => _vm.PrivateValue<TValue>(name);

        /// <summary>
        /// Helper method to set private value of attached view model instance
        /// </summary>
        /// <param name="name">name of the field</param>
        /// <param name="value">new value</param>
        /// <typeparam name="TValue">type</typeparam>
        protected void SetPrivate<TValue>(string name, TValue? value) => _vm.PrivateValueSet(name, value);
    }
}