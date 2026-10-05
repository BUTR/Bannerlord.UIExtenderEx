using Bannerlord.UIExtenderEx.Extensions;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.ViewModels;

/// <summary>
/// Provides the base implementation for <see cref="IViewModelMixin"/> extending a target <see cref="TaleWorlds.Library.ViewModel"/>.
/// <para>
/// Generic type parameter <typeparamref name="TViewModel"/> designates the target host ViewModel type.
/// Access the underlying host instance via the protected <see cref="ViewModel"/> property, which maintains a weak reference
/// to avoid cyclic references and permit garbage collection.
/// </para>
/// </summary>
/// <typeparam name="TViewModel">The <see cref="TaleWorlds.Library.ViewModel"/> type this mixin extends.</typeparam>
public abstract class BaseViewModelMixin<TViewModel> : IViewModelMixin, IViewModelMixinNotifications where TViewModel : ViewModel
{
    private delegate void OnPropertyChangedWithValueDelegate0(ViewModel instance, object value, [CallerMemberName] string? propertyName = null);

    /// <summary>
    /// Invokes the non-generic <c>OnPropertyChangedWithValue(object, string)</c> overload present on older game versions.
    /// Specifying parameter types prevents AmbiguousMatchException warnings against the nine typed overloads.
    /// </summary>
    private static readonly OnPropertyChangedWithValueDelegate0? OnPropertyChangedWithValue0 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate0>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), [typeof(object), typeof(string)], logErrorInTrace: false);

    private static readonly ConcurrentDictionary<Type, OnPropertyChangedWithValueDelegate0?> OnPropertyChangedWithValue1 = new();

    private delegate void OnPropertyChangedWithValueDelegate2(ViewModel instance, bool value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate2? OnPropertyChangedWithValue2 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate2>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), [typeof(bool), typeof(string)]);

    private delegate void OnPropertyChangedWithValueDelegate3(ViewModel instance, int value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate3? OnPropertyChangedWithValue3 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate3>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), [typeof(int), typeof(string)]);

    private delegate void OnPropertyChangedWithValueDelegate4(ViewModel instance, float value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate4? OnPropertyChangedWithValue4 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate4>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), [typeof(float), typeof(string)]);

    private delegate void OnPropertyChangedWithValueDelegate5(ViewModel instance, uint value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate5? OnPropertyChangedWithValue5 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate5>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), [typeof(uint), typeof(string)]);

    private delegate void OnPropertyChangedWithValueDelegate6(ViewModel instance, Color value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate6? OnPropertyChangedWithValue6 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate6>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), [typeof(Color), typeof(string)]);

    private delegate void OnPropertyChangedWithValueDelegate7(ViewModel instance, double value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate7? OnPropertyChangedWithValue7 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate7>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), [typeof(double), typeof(string)]);

    private delegate void OnPropertyChangedWithValueDelegate8(ViewModel instance, Vec2 value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate8? OnPropertyChangedWithValue8 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate8>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), [typeof(Vec2), typeof(string)]);


    private readonly WeakReference<TViewModel> _vm;
    /// <summary>
    /// Gets the attached host <typeparamref name="TViewModel"/> instance, or <see langword="null"/> if the instance
    /// has been garbage-collected.
    /// </summary>
    protected TViewModel? ViewModel => _vm.TryGetTarget(out var vm) ? vm : null;

    protected BaseViewModelMixin(TViewModel vm)
    {
        _vm = new(vm);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        ViewModel?.OnPropertyChanged(propertyName);
    }

    protected void OnPropertyChangedWithValue(object value, [CallerMemberName] string? propertyName = null)
    {
        if (ViewModel is null)
            return;

        if (OnPropertyChangedWithValue0 is not null)
        {
            OnPropertyChangedWithValue0(ViewModel, value, propertyName);
            return;
        }

        switch (value)
        {
            case bool val when OnPropertyChangedWithValue2 is not null: OnPropertyChangedWithValue2(ViewModel, val, propertyName); return;
            case int val when OnPropertyChangedWithValue3 is not null: OnPropertyChangedWithValue3(ViewModel, val, propertyName); return;
            case float val when OnPropertyChangedWithValue4 is not null: OnPropertyChangedWithValue4(ViewModel, val, propertyName); return;
            case uint val when OnPropertyChangedWithValue5 is not null: OnPropertyChangedWithValue5(ViewModel, val, propertyName); return;
            case Color val when OnPropertyChangedWithValue6 is not null: OnPropertyChangedWithValue6(ViewModel, val, propertyName); return;
            case double val when OnPropertyChangedWithValue7 is not null: OnPropertyChangedWithValue7(ViewModel, val, propertyName); return;
            case Vec2 val when OnPropertyChangedWithValue8 is not null: OnPropertyChangedWithValue8(ViewModel, val, propertyName); return;
        }

        static OnPropertyChangedWithValueDelegate0? ValueFactory(Type x)
        {
            var method = AccessTools.GetDeclaredMethods(typeof(ViewModel))
                .FirstOrDefault(m => m.IsGenericMethod && m.Name == nameof(OnPropertyChangedWithValue))?
                .MakeGenericMethod(x);
            return method is null ? null : AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate0>(method);
        }
        if (OnPropertyChangedWithValue1.GetOrAdd(value.GetType(), ValueFactory) is { } del)
        {
            del(ViewModel, value, propertyName);
            return;
        }
    }

    /// <inheritdoc cref="IViewModelMixin.OnRefresh"/>
    public virtual void OnRefresh() { }

    /// <inheritdoc cref="IViewModelMixin.OnFinalize"/>
    public virtual void OnFinalize() { }

    /// <summary>
    /// Invoked whenever the attached host ViewModel raises any property change notification (including standard and typed overloads).
    /// <para>
    /// TaleWorlds ViewModels dispatch most property updates through typed <c>OnPropertyChangedWithValue</c> overloads that
    /// bypass standard <see cref="PropertyChanged"/> handlers. Overriding this method listens to all nine notification variants.
    /// Subscriptions are registered after host construction and detached when the host ViewModel is finalized.
    /// </para>
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected virtual void OnViewModelPropertyChanged(string propertyName) { }

    private static readonly ConcurrentDictionary<Type, bool> HearsNotifications = new();

    private bool _subscribed;

    void IViewModelMixinNotifications.Subscribe()
    {
        if (_subscribed || ViewModel is not { } viewModel)
            return;
        if (!HearsNotifications.GetOrAdd(GetType(), static type =>
                type.GetMethod(nameof(OnViewModelPropertyChanged), BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType is { } declaring
                && !(declaring.IsGenericType && declaring.GetGenericTypeDefinition() == typeof(BaseViewModelMixin<>))))
            return;

        viewModel.PropertyChanged += OnNotified;
        viewModel.PropertyChangedWithValue += OnNotified;
        viewModel.PropertyChangedWithBoolValue += OnNotified;
        viewModel.PropertyChangedWithIntValue += OnNotified;
        viewModel.PropertyChangedWithFloatValue += OnNotified;
        viewModel.PropertyChangedWithUIntValue += OnNotified;
        viewModel.PropertyChangedWithColorValue += OnNotified;
        viewModel.PropertyChangedWithDoubleValue += OnNotified;
        viewModel.PropertyChangedWithVec2Value += OnNotified;
        _subscribed = true;
    }

    void IViewModelMixinNotifications.Unsubscribe()
    {
        if (!_subscribed || ViewModel is not { } viewModel)
            return;

        viewModel.PropertyChanged -= OnNotified;
        viewModel.PropertyChangedWithValue -= OnNotified;
        viewModel.PropertyChangedWithBoolValue -= OnNotified;
        viewModel.PropertyChangedWithIntValue -= OnNotified;
        viewModel.PropertyChangedWithFloatValue -= OnNotified;
        viewModel.PropertyChangedWithUIntValue -= OnNotified;
        viewModel.PropertyChangedWithColorValue -= OnNotified;
        viewModel.PropertyChangedWithDoubleValue -= OnNotified;
        viewModel.PropertyChangedWithVec2Value -= OnNotified;
        _subscribed = false;
    }

    private void OnNotified(object sender, PropertyChangedEventArgs e) => OnViewModelPropertyChanged(e.PropertyName);
    private void OnNotified(object sender, PropertyChangedWithValueEventArgs e) => OnViewModelPropertyChanged(e.PropertyName);
    private void OnNotified(object sender, PropertyChangedWithBoolValueEventArgs e) => OnViewModelPropertyChanged(e.PropertyName);
    private void OnNotified(object sender, PropertyChangedWithIntValueEventArgs e) => OnViewModelPropertyChanged(e.PropertyName);
    private void OnNotified(object sender, PropertyChangedWithFloatValueEventArgs e) => OnViewModelPropertyChanged(e.PropertyName);
    private void OnNotified(object sender, PropertyChangedWithUIntValueEventArgs e) => OnViewModelPropertyChanged(e.PropertyName);
    private void OnNotified(object sender, PropertyChangedWithColorValueEventArgs e) => OnViewModelPropertyChanged(e.PropertyName);
    private void OnNotified(object sender, PropertyChangedWithDoubleValueEventArgs e) => OnViewModelPropertyChanged(e.PropertyName);
    private void OnNotified(object sender, PropertyChangedWithVec2ValueEventArgs e) => OnViewModelPropertyChanged(e.PropertyName);

    /// <summary>
    /// Retrieves a private or internal field or property value from the attached host ViewModel instance.
    /// </summary>
    /// <typeparam name="TValue">The expected value type.</typeparam>
    /// <param name="name">The name of the private field or property.</param>
    /// <returns>The member value, or default if the member cannot be resolved or the host instance is unavailable.</returns>
    protected TValue? GetPrivate<TValue>(string name) => ViewModel.PrivateValue<TValue>(name);

    /// <summary>
    /// Sets a private or internal field or property value on the attached host ViewModel instance.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="name">The name of the private field or property.</param>
    /// <param name="value">The value to assign.</param>
    protected void SetPrivate<TValue>(string name, TValue? value) => ViewModel.PrivateValueSet(name, value);

    /// <summary>
    /// Assigns a new value to a backing field and raises property change notifications if the value changed.
    /// </summary>
    /// <typeparam name="T">The field value type.</typeparam>
    /// <param name="field">A reference to the backing field.</param>
    /// <param name="value">The new value to assign.</param>
    /// <param name="propertyName">The name of the property for change notification.</param>
    /// <returns><see langword="true"/> if the value was modified; otherwise, <see langword="false"/>.</returns>
    protected bool SetField<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}