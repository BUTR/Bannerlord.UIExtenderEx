using System;
using System.Reflection;
using TaleWorlds.Library;
using Debug = System.Diagnostics.Debug;

namespace UIExtenderLib.Interface
{
    /// <summary>
    /// Interface for ViewModel mixins
    /// Should not be used directly, ViewModelMixin<T> should be used as base class
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
    
    /// <summary>
    /// Base class for ViewModelMixin.
    /// Generic parameter T will be used to determine which VM to extend.
    /// You can use protected _vm to access fields of the original view model. 
    /// </summary>
    /// <typeparam name="T">child of ViewModel this mixin is extending</typeparam>
    public class BaseViewModelMixin<T>: IViewModelMixin where T: TaleWorlds.Library.ViewModel
    {
        /// <summary>
        /// ViewModel instance this mixin is attached to
        /// </summary>
        protected WeakReference<T> _vm;
        
        public BaseViewModelMixin(T vm)
        {
            _vm = new WeakReference<T>(vm);
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
        /// <typeparam name="V">type</typeparam>
        /// <returns></returns>
        protected V GetPrivate<V>(string name)
        {
            return _vm.PrivateValue<V>(name);
        }

        /// <summary>
        /// Helper method to set private value of attached view model instance
        /// </summary>
        /// <param name="name">name of the field</param>
        /// <param name="value">new value</param>
        /// <typeparam name="V">type</typeparam>
        protected void SetPrivate<V>(string name, V value)
        {
            _vm.PrivateValueSet(name, value);
        }
    }
}