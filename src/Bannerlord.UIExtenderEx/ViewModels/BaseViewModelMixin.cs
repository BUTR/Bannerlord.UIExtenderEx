using Bannerlord.UIExtenderEx.Extensions;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using TaleWorlds.GauntletUI;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.ViewModels;

/// <summary>
/// Basic implementation for <see cref="IViewModelMixin"/>.<br/>
/// Generic parameter <typeparamref name="TViewModel"/> will be used to determine which <see cref="TaleWorlds.Library.ViewModel"/> to extend.<br/>
/// You can use the field <see cref="BaseViewModelMixin{TViewModel}.ViewModel"/> to access the original <see cref="TaleWorlds.Library.ViewModel"/>.<br/>
/// Be aware that it might be null if GC has disposed the original <see cref="ViewModel"/>. The mixin holds a weak reference to it.
/// </summary>
/// <typeparam name="TViewModel"><see cref="TaleWorlds.Library.ViewModel"/> this mixin is extending.</typeparam>
public abstract class BaseViewModelMixin<TViewModel> : IViewModelMixin where TViewModel : ViewModel
{
    private delegate void OnPropertyChangedWithValueDelegate0(ViewModel instance, object value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate0? OnPropertyChangedWithValue0 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate0>(typeof(ViewModel), nameof(OnPropertyChangedWithValue));

    private static readonly ConcurrentDictionary<Type, OnPropertyChangedWithValueDelegate0?> OnPropertyChangedWithValue1 = new();

    private delegate void OnPropertyChangedWithValueDelegate2(ViewModel instance, bool value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate2? OnPropertyChangedWithValue2 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate2>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), new[] { typeof(bool), typeof(string) });

    private delegate void OnPropertyChangedWithValueDelegate3(ViewModel instance, int value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate3? OnPropertyChangedWithValue3 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate3>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), new[] { typeof(int), typeof(string) });

    private delegate void OnPropertyChangedWithValueDelegate4(ViewModel instance, float value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate4? OnPropertyChangedWithValue4 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate4>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), new[] { typeof(float), typeof(string) });

    private delegate void OnPropertyChangedWithValueDelegate5(ViewModel instance, uint value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate5? OnPropertyChangedWithValue5 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate5>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), new[] { typeof(uint), typeof(string) });

    private delegate void OnPropertyChangedWithValueDelegate6(ViewModel instance, Color value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate6? OnPropertyChangedWithValue6 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate6>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), new[] { typeof(Color), typeof(string) });

    private delegate void OnPropertyChangedWithValueDelegate7(ViewModel instance, double value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate7? OnPropertyChangedWithValue7 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate7>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), new[] { typeof(double), typeof(string) });

    private delegate void OnPropertyChangedWithValueDelegate8(ViewModel instance, Vec2 value, [CallerMemberName] string? propertyName = null);
    private static readonly OnPropertyChangedWithValueDelegate8? OnPropertyChangedWithValue8 =
        AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate8>(typeof(ViewModel), nameof(OnPropertyChangedWithValue), new[] { typeof(Vec2), typeof(string) });


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

        static OnPropertyChangedWithValueDelegate0 ValueFactory(Type x) => AccessTools2.GetDelegate<OnPropertyChangedWithValueDelegate0>(AccessTools.GetDeclaredMethods(typeof(ViewModel))
            .FirstOrDefault(x => x.IsGenericMethod && x.Name == nameof(OnPropertyChangedWithValue))?
            .MakeGenericMethod(x));
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