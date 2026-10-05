using System;
using System.Collections.Generic;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Runtimes;

/// <summary>
/// Queries active and target ViewModel mixin types registered across all module runtimes, mirroring Gauntlet XML databinding resolution.
/// </summary>
public static class MixinSource
{
    /// <summary>
    /// Returns enabled mixin types registered for the exact specified ViewModel type, ordered by module registration sequence
    /// followed by mixin declaration order (matching property/command shadowing semantics).
    /// </summary>
    public static IEnumerable<Type> GetEnabledMixinTypes(Type viewModelType) =>
        UIExtender.GetAllRuntimes().SelectMany(x => x.ViewModelComponent.GetEnabledMixinTypes(viewModelType));

    /// <summary>
    /// Returns all distinct ViewModel types targeted by registered mixins, regardless of whether the mixins are currently enabled.
    /// </summary>
    public static IEnumerable<Type> GetMixinTargetTypes() =>
        UIExtender.GetAllRuntimes().SelectMany(x => x.ViewModelComponent.GetMixinTargetTypes());
}