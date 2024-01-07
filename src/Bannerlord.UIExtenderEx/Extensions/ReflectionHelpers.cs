using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Bannerlord.UIExtenderEx.Extensions;

internal static class ReflectionHelpers
{
    private delegate void SetMemberValue<in T>(object instance, T? value);
    private delegate T? GetMemberValue<out T>(object instance);

    private static readonly ConcurrentDictionary<Type, Dictionary<string, MemberInfo>> _fieldPropertyCache = new();
    private static readonly ConcurrentDictionary<MemberInfo, Delegate?> _getDelegateCache = new();
    private static readonly ConcurrentDictionary<MemberInfo, Delegate?> _setDelegateCache = new();

    /// <summary>
    /// Can be null
    /// </summary>
    public static T? PrivateValue<T>(this object? o, string fieldPropertyName)
    {
        if (o is null) return default;

        var membersCache = _fieldPropertyCache.GetOrAdd(o.GetType(), static x => x.GetProperties().OfType<MemberInfo>().Concat(x.GetFields()).ToDictionary(x => x.Name, x => x));
        if (!membersCache.TryGetValue(fieldPropertyName, out var member)) return default;
        var @delegate = _getDelegateCache.GetOrAdd(member, static x => x switch
        {
            FieldInfo fieldInfo => AccessTools2.FieldRefAccess<object, T>(fieldInfo.Name),
            PropertyInfo propertyInfo => AccessTools2.GetPropertyGetterDelegate<GetMemberValue<T>>(propertyInfo),
            var _ => null
        });
        switch (@delegate)
        {
            case GetMemberValue<T> del:
                return del(o);
            case AccessTools.FieldRef<object, T> del:
                return del(o);
            case var _:
                return default;
        }
    }

    public static void PrivateValueSet<T>(this object? o, string fieldPropertyName, T? value)
    {
        if (o is null) return;

        var membersCache = _fieldPropertyCache.GetOrAdd(o.GetType(), static x => x.GetProperties().OfType<MemberInfo>().Concat(x.GetFields()).ToDictionary(x => x.Name, x => x));
        if (!membersCache.TryGetValue(fieldPropertyName, out var member)) return;
        var @delegate = _setDelegateCache.GetOrAdd(member, static x => x switch
        {
            FieldInfo fieldInfo => AccessTools2.FieldRefAccess<object, T?>(fieldInfo.Name),
            PropertyInfo propertyInfo => AccessTools2.GetPropertySetterDelegate<SetMemberValue<T>>(propertyInfo),
            var _ => null
        });
        switch (@delegate)
        {
            case SetMemberValue<T> del:
                del(o, value);
                break;
            case AccessTools.FieldRef<object, T> del:
                del(o) = value;
                break;
        }
    }
}