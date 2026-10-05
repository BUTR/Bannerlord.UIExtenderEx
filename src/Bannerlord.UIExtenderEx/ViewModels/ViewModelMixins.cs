using System;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.ViewModels;

/// <summary>
/// Provides lookup APIs for retrieving mixin instances attached to a <see cref="ViewModel"/>.
/// <para>
/// Emitted in compiled prefab C# to resolve mixin-bound properties and commands with direct, typed invocation speed.
/// </para>
/// </summary>
public static class ViewModelMixins
{
    /// <summary>
    /// Retrieves the <typeparamref name="TMixin"/> instance attached to <paramref name="viewModel"/>, or <see langword="null"/>
    /// if no matching mixin is attached or <paramref name="viewModel"/> is <see langword="null"/>. Attached mixin instances
    /// remain queryable even if disabled post-attachment.
    /// </summary>
    /// <typeparam name="TMixin">The target mixin type.</typeparam>
    /// <param name="viewModel">The host ViewModel instance.</param>
    /// <returns>The attached mixin instance, or <see langword="null"/>.</returns>
    public static TMixin? Get<TMixin>(ViewModel? viewModel) where TMixin : class, IViewModelMixin
    {
        if (viewModel is null)
            return null;

        foreach (var runtime in UIExtender.GetAllRuntimes())
        {
            if (!runtime.ViewModelComponent.MixinInstanceCache.TryGetValue(viewModel, out var mixins))
                continue;

            foreach (var mixin in mixins)
            {
                if (mixin is TMixin typed)
                    return typed;
            }
        }

        return null;
    }

    /// <summary>
    /// Evaluates a member selector delegate against the <typeparamref name="TMixin"/> instance attached to <paramref name="viewModel"/>,
    /// returning default if the mixin is absent or <paramref name="viewModel"/> is <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TMixin">The target mixin type.</typeparam>
    /// <typeparam name="TValue">The selected return value type.</typeparam>
    /// <param name="viewModel">The host ViewModel instance.</param>
    /// <param name="selector">The delegate selecting a member value on the mixin.</param>
    /// <returns>The selected value, or <see langword="default"/>.</returns>
    public static TValue? Select<TMixin, TValue>(ViewModel? viewModel, Func<TMixin, TValue> selector) where TMixin : class, IViewModelMixin
    {
        var mixin = Get<TMixin>(viewModel);
        return mixin is null ? default : selector(mixin);
    }
}