using System.Reflection;
using TaleWorlds.Library;
using Debug = System.Diagnostics.Debug;

namespace UIExtenderLib.ViewModel
{
    /**
     * Interface for ViewModel mixins
     * Should not be used directly, ViewModelMixin<T> should be used as base class
     */
    public interface IViewModelMixin
    {
        void Refresh();
    }
    
    /**
     * Base class for ViewModelMixin.
     * Generic parameter T will be used to determine which VM to extend.
     * You can use protected _vm to access fields of the original view model.
     */
    public class BaseViewModelMixin<T>: IViewModelMixin where T: TaleWorlds.Library.ViewModel
    {
        // view model this mixin is attached to
        protected T _vm;
        
        public BaseViewModelMixin(T vm)
        {
            _vm = vm;
        }
        
        /**
         * Called when view model is refreshed
         */
        public virtual void Refresh() { }

        /**
         * Helper method to get private value from _vm
         */
        protected V GetPrivate<V>(string name)
        {
            var field = _vm.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Debug.Assert(field != null, $"Type {GetType()} doesn't have non-public field {name}!'");
            return (V) field.GetValue(_vm);
        }

        /**
         * Helper method to set private value from _vm
         */
        protected void SetPrivate<V>(string name, V value)
        {
            var field = _vm.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Debug.Assert(field != null, $"Type {GetType()} doesn't have non-public field {name}!'");
            field.SetValue(_vm, value);
        }
    }
}