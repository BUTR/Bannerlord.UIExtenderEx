using Bannerlord.UIExtenderEx.Extensions;

using System;

namespace Bannerlord.UIExtenderEx.ViewModels
{
    /// <summary>
    /// Basic implementation for <see cref="IViewModelMixin"/>.<br/>
    /// Generic parameter <typeparamref name="TViewModel"/> will be used to determine which <see cref="TaleWorlds.Library.ViewModel"/> to extend.<br/>
    /// You can use the field <see cref="BaseViewModelMixin{TViewModel}.ViewModel"/> to access the original <see cref="TaleWorlds.Library.ViewModel"/>.<br/>
    /// Be aware that it might be null if GC has disposed the original <see cref="ViewModel"/>. The mixin holds a weak reference to it.
    /// </summary>
    /// <typeparam name="TViewModel"><see cref="TaleWorlds.Library.ViewModel"/> this mixin is extending.</typeparam>
    public abstract class BaseViewModelMixin<TViewModel> : IViewModelMixin where TViewModel : TaleWorlds.Library.ViewModel
    {
        private readonly WeakReference<TViewModel> _vm;
        /// <summary>
        /// The original <see cref="TaleWorlds.Library.ViewModel"/>.<br/>
        /// Be aware that it might be null if GC has disposed the original <see cref="ViewModel"/>. The mixin holds a weak reference to it.
        /// </summary>
        protected TViewModel? ViewModel => _vm.TryGetTarget(out var vm) ? vm : null;

        protected BaseViewModelMixin(TViewModel vm)
        {
            _vm = new WeakReference<TViewModel>(vm);
        }
        
        /// <inheritdoc cref="IViewModelMixin.OnRefresh"/>
        public virtual void OnRefresh() { }
        
        /// <inheritdoc cref="IViewModelMixin.OnFinalize"/>
        public virtual void OnFinalize() { }

        /// <summary>
        /// Helper method to get non public value from attached view model instance.
        /// </summary>
        /// <param name="name">name of the field</param>
        /// <typeparam name="TValue">type</typeparam>
        /// <returns></returns>
        protected TValue? GetPrivate<TValue>(string name) => _vm.PrivateValue<TValue>(name);

        /// <summary>
        /// Helper method to set non public value of attached view model instance.
        /// </summary>
        /// <param name="name">name of the member to set</param>
        /// <param name="value">new value</param>
        /// <typeparam name="TValue">member type</typeparam>
        protected void SetPrivate<TValue>(string name, TValue? value) => _vm.PrivateValueSet(name, value);
    }
}