using System;

using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Resolves static data source paths across view model types and binding lists, mirroring <c>ViewModel.GetViewModelAtPath</c>.
/// </summary>
internal static class ViewModelPaths
{
    /// <summary>
    /// Resolves the static CLR type reached by navigating <paramref name="path"/> from the specified starting <paramref name="type"/>.
    /// Returns <see langword="null"/> if the path navigates into an unresolvable or dynamic member.
    /// </summary>
    /// <param name="type">The starting view model CLR type.</param>
    /// <param name="path">The binding path to navigate.</param>
    /// <returns>The resolved destination <see cref="Type"/>, or <see langword="null"/> if unmapped.</returns>
    public static Type? GetTypeAtPath(Type? type, BindingPath path)
    {
        // Above the class's root, in a scope of whatever hosts it: no declared type is known there, and what is bound there
        // goes by name, as the loader goes
        if (OuterPath.IsOuter(path))
        {
            return null;
        }
        if (path.SubPath is not { } subPath)
        {
            return type;
        }
        // The type the property declares; whether its getter is public was settled by the lookup, which only answers public
        // properties, as ViewModel.GetPropertyValue only reads public getters
        var returnType = ViewModelMemberResolution.GetProperty(type, subPath.FirstNode, out _)?.PropertyType;
        if (returnType is null)
        {
            return null;
        }
        if (typeof(ViewModel).IsAssignableFrom(returnType))
        {
            return GetTypeAtPath(returnType, subPath);
        }
        if (typeof(IMBBindingList).IsAssignableFrom(returnType))
        {
            return GetTypeInList(returnType, subPath);
        }
        return null;
    }

    private static Type? GetTypeInList(Type bindingListType, BindingPath path)
    {
        if (path.SubPath is not { } subPath)
        {
            return bindingListType;
        }
        // Not GetGenericArguments()[0]. A list reached through IMBBindingList or through a non-generic subclass of
        // MBBindingList<T> has no generic argument of its own, and indexing into an empty array took the movie down with an
        // IndexOutOfRangeException that named nothing.
        if (GetElementType(bindingListType) is not { } type)
        {
            return null;
        }
        if (typeof(ViewModel).IsAssignableFrom(type))
        {
            return GetTypeAtPath(type, subPath);
        }
        if (typeof(IMBBindingList).IsAssignableFrom(type))
        {
            return GetTypeInList(type, subPath);
        }
        return null;
    }

    /// <summary>
    /// Resolves the element type of the specified collection type implementing <see cref="IMBBindingList"/>.
    /// </summary>
    /// <param name="bindingListType">The collection type to inspect.</param>
    /// <returns>The resolved element <see cref="Type"/>, or <see langword="null"/> if non-generic.</returns>
    public static Type? GetElementType(Type bindingListType)
    {
        TypeDependencies.Inspect(bindingListType);
        for (var type = bindingListType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericArguments() is { Length: 1 } arguments)
            {
                return arguments[0];
            }
        }
        foreach (var interfaceType in bindingListType.GetInterfaces())
        {
            if (interfaceType.IsGenericType && typeof(IMBBindingList).IsAssignableFrom(interfaceType)
                && interfaceType.GetGenericArguments() is { Length: 1 } arguments)
            {
                return arguments[0];
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the first segment along <paramref name="path"/> that names a declared but inaccessible (non-public) member.
    /// </summary>
    /// <param name="type">The view model type.</param>
    /// <param name="path">The navigation path to inspect.</param>
    /// <returns>The name of the inaccessible member segment if found; otherwise, <see langword="null"/>.</returns>
    public static string? FindUnusableStep(Type? type, BindingPath path)
    {
        // No type left to ask, or nothing left to walk; above the class's root, nothing is declared to ask
        if (OuterPath.IsOuter(path) || path.SubPath is not { } subPath || type is null)
        {
            return null;
        }

        var node = subPath.FirstNode;
        if (ViewModelMemberResolution.GetProperty(type, node, out _) is not { } property)
        {
            return ViewModelMemberResolution.DeclaresNoMember(type, node) ? null : node;
        }

        var returnType = property.PropertyType;
        if (typeof(ViewModel).IsAssignableFrom(returnType))
        {
            return FindUnusableStep(returnType, subPath);
        }
        if (typeof(IMBBindingList).IsAssignableFrom(returnType))
        {
            // The next node is an index, not a member, so nothing about it can be unusable. Only what lies beyond it, on the
            // element type, can be, and a path that stops at the list has nothing beyond it at all.
            return subPath.SubPath is null ? null : FindUnusableStep(GetElementType(returnType), subPath.SubPath);
        }
        return null;
    }
}