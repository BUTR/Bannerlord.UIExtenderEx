using Bannerlord.BUTR.Shared.Utils;
using Bannerlord.UIExtenderEx.Extensions;
using Bannerlord.UIExtenderEx.ViewModels;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx
{
    // TODO: v3

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal class BUTRDataSourcePropertyAttribute : Attribute
    {
        public string? OverrideName { get; set; }
    }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal class BUTRDataSourceMethodAttribute : Attribute
    {
        public string? OverrideName { get; set; }
    }

    /// <summary>
    /// A custom ViewModel with the static property/method cache disabled entirely.
    /// Also, has it's own property/method discovery mechanism
    /// </summary>
    internal abstract class BUTRViewModel : ViewModel
    {
        protected BUTRViewModel()
        {
            var properties = GetType().GetProperties(AccessTools.all);
            foreach (var propertyInfo in properties)
            {
                if (propertyInfo.GetCustomAttribute<BUTRDataSourcePropertyAttribute>() is { } attribute)
                {
                    if (propertyInfo.GetMethod?.IsPrivate == true || propertyInfo.SetMethod?.IsPrivate == true) throw new Exception();

                    this.AddProperty(attribute.OverrideName ?? propertyInfo.Name, propertyInfo);
                }
            }

            var methods = GetType().GetMethods(AccessTools.all);
            foreach (var methodInfo in methods)
            {
                if (methodInfo.GetCustomAttribute<BUTRDataSourceMethodAttribute>() is { } attribute)
                {
                    if (methodInfo.IsPrivate) throw new Exception();

                    this.AddMethod(attribute.OverrideName ?? methodInfo.Name, methodInfo);
                }
            }
        }
    }

    /// <summary>
    /// An advanced ViewModel mixin with the same discovery mechanism as BUTRViewModel
    /// </summary>
    /// <typeparam name="TViewModelMixin"></typeparam>
    /// <typeparam name="TViewModel"></typeparam>
    internal abstract class BUTRViewModelMixin<TViewModelMixin, TViewModel> : IViewModelMixin
        where TViewModelMixin : BUTRViewModelMixin<TViewModelMixin, TViewModel>
        where TViewModel : ViewModel
    {
        private readonly WeakReference<TViewModel> _vm;

        protected TViewModel? ViewModel => _vm.TryGetTarget(out var vm) ? vm : null;

        public TViewModelMixin Mixin => (TViewModelMixin) this;

        protected BUTRViewModelMixin(TViewModel vm)
        {
            _vm = new WeakReference<TViewModel>(vm);

            SetVMProperty(nameof(Mixin), GetType().Name);
            foreach (var propertyInfo in GetType().GetProperties(AccessTools.all))
            {
                if (propertyInfo.GetCustomAttribute<BUTRDataSourcePropertyAttribute>() is { } attribute)
                {
                    if (propertyInfo.GetMethod?.IsPrivate == true || propertyInfo.SetMethod?.IsPrivate == true) throw new Exception();

                    var wrappedPropertyInfo = new WrappedPropertyInfo(propertyInfo, this);
                    vm.AddProperty(attribute.OverrideName ?? propertyInfo.Name, wrappedPropertyInfo);
                    wrappedPropertyInfo.PropertyChanged += (_, e) => ViewModel?.OnPropertyChanged(e.PropertyName);
                }
            }
            foreach (var methodInfo in GetType().GetMethods(AccessTools.all))
            {
                if (methodInfo.GetCustomAttribute<BUTRDataSourceMethodAttribute>() is { } attribute)
                {
                    if (methodInfo.IsPrivate) throw new Exception();

                    var wrappedMethodInfo = new WrappedMethodInfo(methodInfo, this);
                    vm.AddMethod(attribute.OverrideName ?? methodInfo.Name, wrappedMethodInfo);
                }
            }
        }

        /// <inheritdoc />
        public abstract void OnRefresh();

        /// <inheritdoc />
        public abstract void OnFinalize();

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            var t = ViewModel?.GetPropertyValue(propertyName);
            ViewModel?.OnPropertyChanged(propertyName);
        }

        protected void OnPropertyChangedWithValue(object? value, [CallerMemberName] string? propertyName = null)
        {
            ViewModel?.OnPropertyChangedWithValue(value, propertyName);
        }

        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            OnPropertyChangedWithValue(value, propertyName);
            return true;
        }

        protected void SetVMProperty(string property, string? overrideName = null)
        {
            var propertyInfo = new WrappedPropertyInfo(AccessTools2.Property(GetType(), property)!, this);
            ViewModel?.AddProperty(overrideName ?? property, propertyInfo);
            propertyInfo.PropertyChanged += (_, e) => ViewModel?.OnPropertyChanged(e.PropertyName);
        }

        protected void SetVMMethod(string method, string? overrideName = null)
        {
            var methodInfo = new WrappedMethodInfo(AccessTools2.Method(GetType(), method)!, this);
            ViewModel?.AddMethod(overrideName ?? method, methodInfo);
        }
    }
}