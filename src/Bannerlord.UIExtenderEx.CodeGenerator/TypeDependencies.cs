using System;
using System.Collections.Generic;
using System.Reflection;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;

/// <summary>
/// Records reflection types inspected by the code generator during code emission while a <see cref="Recording"/> session is active on the current thread.
/// </summary>
/// <remarks>
/// <para>
/// Tracks all types that influence emission decisions to ensure compiled assembly dependencies remain valid.
/// Records types examined across <see cref="ViewModelMemberResolution"/>, property path binding walks, <see cref="Widgets.WidgetPropertyPath"/>, and widget class lookups.
/// Member lookups and overload resolution evaluate candidate methods, parameter types, return types, and constructors.
/// </para>
/// <para>
/// When no active recording session is present, all inspection methods execute as no-ops.
/// </para>
/// </remarks>
public static class TypeDependencies
{
    [ThreadStatic]
    private static HashSet<Type>? _types;

    /// <summary>
    /// Begins a new type dependency recording scope on the current thread.
    /// </summary>
    public static Recording Begin() => new();

    /// <summary>
    /// Records the specified <see cref="Type"/> as an inspected dependency.
    /// </summary>
    public static void Inspect(Type? type)
    {
        if (type is not null)
        {
            _types?.Add(type);
        }
    }

    /// <summary>Records the property's declaring type and property type as inspected dependencies.</summary>
    public static void Inspect(PropertyInfo? property)
    {
        if (property is not null && _types is not null)
        {
            Inspect(property.DeclaringType);
            Inspect(property.PropertyType);
        }
    }

    /// <summary>
    /// Records the method's declaring type, return type, and parameter types as inspected dependencies.
    /// </summary>
    public static void Inspect(MethodInfo? method)
    {
        if (method is not null && _types is not null)
        {
            Inspect(method.DeclaringType);
            Inspect(method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                Inspect(parameter.ParameterType);
            }
        }
    }

    /// <summary>
    /// Records all method overloads matching the specified name on the target type to capture overload resolution candidates.
    /// </summary>
    public static void InspectOverloads(Type? type, string methodName)
    {
        if (type is null || _types is null)
        {
            return;
        }
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        foreach (var method in type.GetMethods(all))
        {
            if (method.Name == methodName)
            {
                Inspect(method);
            }
        }
    }

    /// <summary>Records constructor parameter types on the target type to capture constructor resolution candidates.</summary>
    public static void InspectConstructors(Type? type)
    {
        if (type is null || _types is null)
        {
            return;
        }
        foreach (var constructor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                Inspect(parameter.ParameterType);
            }
        }
    }

    /// <summary>Represents a scoped recording session tracking inspected types on the active thread.</summary>
    public sealed class Recording : IDisposable
    {
        private readonly HashSet<Type>? _outer;
        private readonly HashSet<Type> _types = [];

        internal Recording()
        {
            _outer = TypeDependencies._types;
            TypeDependencies._types = _types;
        }

        public IReadOnlyCollection<Type> Types => _types;

        public void Dispose() => TypeDependencies._types = _outer;
    }
}