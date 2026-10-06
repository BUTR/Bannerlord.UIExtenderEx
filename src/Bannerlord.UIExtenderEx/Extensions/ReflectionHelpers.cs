using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Bannerlord.UIExtenderEx.Extensions;

/// <summary>
/// Provides cached reflection and compiled expression-tree accessors for reading and writing private members on host objects.
/// </summary>
internal static class ReflectionHelpers
{
    /// <summary>
    /// Caches member lookups across type hierarchies. Properties take precedence over fields of the same name.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type Type, string Name), MemberInfo?> Members = new();

    private static MemberInfo? FindMember(Type type, string name) => Members.GetOrAdd((type, name), static key =>
    {
        // No property is expected for a field; neither is reported, once per type and name, as reads then give default and writes do nothing
        if (((MemberInfo?) AccessTools2.Property(key.Type, key.Name, logErrorInTrace: false) ?? AccessTools2.Field(key.Type, key.Name, logErrorInTrace: false)) is { } member)
            return member;

        Trace.TraceWarning("UIExtenderEx: {0} has no field or property {1}; GetPrivate reads it as default and SetPrivate writes nothing", key.Type.FullName, key.Name);
        return null;
    });

    /// <summary>
    /// Caches compiled expression-tree getter and setter delegates per member and value type to eliminate reflection overhead in polled UI paths.
    /// </summary>
    private static class Accessors<T>
    {
        public static readonly ConcurrentDictionary<MemberInfo, Func<object, T?>?> Getters = new();
        public static readonly ConcurrentDictionary<MemberInfo, Action<object, T?>?> Setters = new();
    }

    extension(object? o)
    {
        /// <summary>
        /// Retrieves the value of a private or non-public field or property on the target instance.
        /// </summary>
        public T? PrivateValue<T>(string fieldPropertyName)
        {
            if (o is null || FindMember(o.GetType(), fieldPropertyName) is not { } member) return default;

            var getter = Accessors<T>.Getters.GetOrAdd(member, CreateGetter<T>);
            return getter is null ? default : getter(o);
        }

        /// <summary>
        /// Sets the value of a private or non-public field or property on the target instance.
        /// </summary>
        public void PrivateValueSet<T>(string fieldPropertyName, T? value)
        {
            if (o is null || FindMember(o.GetType(), fieldPropertyName) is not { } member) return;

            Accessors<T>.Setters.GetOrAdd(member, CreateSetter<T>)?.Invoke(o, value);
        }
    }

    private static Func<object, T?>? CreateGetter<T>(MemberInfo member)
    {
        if (member is PropertyInfo { CanRead: false } || MemberType(member) is not { } memberType) return null;

        // Fall back to reflection if the member type is broader than T (e.g. an object field holding a string value)
        Func<object, T?> reflection = instance => GetValue(member, instance) is T typed ? typed : default;
        if (!IsInstanceMemberOfClass(member) || !typeof(T).IsAssignableFrom(memberType)) return reflection;

        try
        {
            var instance = Expression.Parameter(typeof(object));
            var access = Expression.MakeMemberAccess(Expression.Convert(instance, member.DeclaringType!), member);
            return Expression.Lambda<Func<object, T?>>(Expression.Convert(access, typeof(T)), instance).Compile();
        }
        catch (Exception)
        {
            return reflection;
        }
    }

    private static Action<object, T?>? CreateSetter<T>(MemberInfo member)
    {
        if (member is PropertyInfo { CanWrite: false } || MemberType(member) is not { } memberType) return null;

        Action<object, T?> reflection = (instance, value) => SetValue(member, instance, value);
        // A readonly field can only be written through reflection
        if (!IsInstanceMemberOfClass(member) || member is FieldInfo { IsInitOnly: true } || !memberType.IsAssignableFrom(typeof(T))) return reflection;

        try
        {
            var instance = Expression.Parameter(typeof(object));
            var value = Expression.Parameter(typeof(T));
            var access = Expression.MakeMemberAccess(Expression.Convert(instance, member.DeclaringType!), member);
            return Expression.Lambda<Action<object, T?>>(Expression.Assign(access, Expression.Convert(value, memberType)), instance, value).Compile();
        }
        catch (Exception)
        {
            return reflection;
        }
    }

    private static Type? MemberType(MemberInfo member) => member switch
    {
        PropertyInfo property => property.PropertyType,
        FieldInfo field => field.FieldType,
        _ => null,
    };

    /// <summary>A static member, or one of a struct the instance is a boxed copy of, is left to reflection.</summary>
    private static bool IsInstanceMemberOfClass(MemberInfo member) => member.DeclaringType is { IsValueType: false } && member switch
    {
        PropertyInfo property => !(property.GetMethod ?? property.SetMethod)!.IsStatic,
        FieldInfo field => !field.IsStatic,
        _ => false,
    };

    private static object? GetValue(MemberInfo member, object instance) => member switch
    {
        PropertyInfo property => property.GetValue(instance),
        FieldInfo field => field.GetValue(instance),
        _ => null,
    };

    private static void SetValue(MemberInfo member, object instance, object? value)
    {
        switch (member)
        {
            case PropertyInfo property:
                property.SetValue(instance, value);
                break;
            case FieldInfo field:
                field.SetValue(instance, value);
                break;
        }
    }
}