using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Resolves properties and methods contributed to a target <see cref="ViewModel"/> by enabled view model mixins during code generation.
/// <para>
/// Mirrors the dynamic binding semantics of <see cref="ViewModelSource"/>: requires exact ViewModel type matching,
/// inspects enabled mixins, respects registration order (later mixins override earlier ones), and only binds members
/// explicitly decorated with <see cref="DataSourceProperty"/> or <see cref="DataSourceMethodAttribute"/>.
/// </para>
/// </summary>
public sealed class MixinMemberResolver : IViewModelMemberResolver
{
    /// <summary>Resolves a mixin-contributed property for the specified ViewModel type and member name.</summary>
    public PropertyInfo? ResolveProperty(Type viewModelType, string propertyName, out Type? mixinType)
    {
        PropertyInfo? result = null;
        mixinType = null;

        foreach (var mixin in GetEnabledMixinTypes(viewModelType))
        {
            // Asked whether or not it answers: a mixin gaining the member later changes what is generated
            TypeDependencies.Inspect(mixin);
            var property = mixin.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(x => x.Name == propertyName && IsMarked(x, typeof(DataSourceProperty)));
            if (property is null)
                continue;

            result = property;
            mixinType = mixin;
        }

        return result;
    }

    /// <summary>Resolves a mixin-contributed method command for the specified ViewModel type and method name.</summary>
    public MethodInfo? ResolveMethod(Type viewModelType, string methodName, out Type? mixinType)
    {
        MethodInfo? result = null;
        mixinType = null;

        foreach (var mixin in GetEnabledMixinTypes(viewModelType))
        {
            TypeDependencies.Inspect(mixin);
            var method = mixin.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(x => x.Name == methodName && IsMarked(x, typeof(DataSourceMethodAttribute)));
            if (method is null)
                continue;

            result = method;
            mixinType = mixin;
        }

        return result;
    }

    private static bool IsMarked(MemberInfo member, Type attribute) => member.CustomAttributes.Any(x => x.AttributeType == attribute);

    /// <summary>
    /// Retrieves enabled mixin types registered for the specified ViewModel type in deterministic resolution order.
    /// </summary>
    private static IEnumerable<Type> GetEnabledMixinTypes(Type viewModelType) => MixinRegistrations.ForViewModel(viewModelType);
}