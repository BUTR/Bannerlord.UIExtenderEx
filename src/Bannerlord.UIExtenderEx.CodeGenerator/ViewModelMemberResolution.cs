using System;
using System.Reflection;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;

/// <summary>
/// Resolves ViewModel members contributed outside direct reflection types, such as properties and commands introduced by ViewModel mixins.
/// </summary>
/// <remarks>
/// Resolved mixin members are accessed in generated C# via <c>Bannerlord.UIExtenderEx.ViewModels.ViewModelMixins.Get&lt;TMixin&gt;(viewModel)</c>.
/// </remarks>
public interface IViewModelMemberResolver
{
    PropertyInfo? ResolveProperty(Type viewModelType, string propertyName, out Type? mixinType);

    MethodInfo? ResolveMethod(Type viewModelType, string methodName, out Type? mixinType);
}

/// <summary>
/// Provides shared reflection lookups and type name formatting for ViewModel databinding code emission.
/// </summary>
/// <remarks>
/// Prioritizes mixin members supplied by <see cref="Resolver"/> over base ViewModel members to mirror TaleWorlds binding table precedence.
/// </remarks>
public static class ViewModelMemberResolution
{
    /// <summary>
    /// Restricts property lookup to public instance properties conforming to XML loader databinding requirements.
    /// </summary>
    /// <remarks>
    /// <c>ViewModel.GetPropertyValue</c> and <c>ViewModel.SetPropertyValue</c> require public get/set accessors; non-public properties cannot be bound by the XML loader.
    /// </remarks>
    private const BindingFlags PropertyFlags = BindingFlags.Instance | BindingFlags.Public;

    /// <summary>
    /// Configures method binding flags to include non-public instance methods conforming to <c>ViewModel.ExecuteCommand</c> execution semantics.
    /// </summary>
    private const BindingFlags MethodFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static IViewModelMemberResolver? Resolver { get; set; }

    /// <summary>
    /// Resolves a property on the specified ViewModel type or its registered mixins, giving precedence to mixin properties.
    /// </summary>
    /// <param name="viewModelType">The ViewModel type to inspect.</param>
    /// <param name="propertyName">The property name to resolve.</param>
    /// <param name="mixinType">Outputs the mixin type if the property was resolved on a mixin; otherwise, <see langword="null"/>.</param>
    /// <returns>The resolved <see cref="PropertyInfo"/>, or <see langword="null"/> if not found.</returns>
    /// <remarks>
    /// Mirrors the XML loader's property resolution precedence by evaluating mixin binding tables before base ViewModel properties.
    /// </remarks>
    public static PropertyInfo? GetProperty(Type? viewModelType, string propertyName, out Type? mixinType)
    {
        mixinType = null;
        if (viewModelType == null)
        {
            return null;
        }
        TypeDependencies.Inspect(viewModelType);
        if (Resolver?.ResolveProperty(viewModelType, propertyName, out mixinType) is { } fromMixin)
        {
            TypeDependencies.Inspect(mixinType);
            TypeDependencies.Inspect(fromMixin);
            return fromMixin;
        }
        mixinType = null;
        var properties = viewModelType.GetProperties(PropertyFlags);
        foreach (var propertyInfo in properties)
        {
            if (propertyInfo.Name == propertyName)
            {
                TypeDependencies.Inspect(propertyInfo);
                return propertyInfo;
            }
        }
        return null;
    }

    /// <summary>
    /// Determines whether the ViewModel type declares or inherits an instance property matching the specified name.
    /// </summary>
    /// <param name="viewModelType">The ViewModel type to inspect.</param>
    /// <param name="propertyName">The property name to check.</param>
    /// <returns><see langword="true"/> if the property exists in the ViewModel binding table; otherwise, <see langword="false"/>.</returns>
    public static bool ViewModelAnswersProperty(Type? viewModelType, string propertyName)
    {
        if (viewModelType == null)
        {
            return false;
        }
        TypeDependencies.Inspect(viewModelType);
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var propertyInfo in viewModelType.GetProperties(all))
        {
            if (propertyInfo.Name == propertyName && propertyInfo.GetIndexParameters().Length == 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Determines whether the ViewModel type or any base type defines an executable method matching the specified name.
    /// </summary>
    public static bool ViewModelAnswersCommand(Type? viewModelType, string methodName)
    {
        const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        TypeDependencies.Inspect(viewModelType);
        for (var type = viewModelType; type != null; type = type.BaseType)
        {
            foreach (var methodInfo in type.GetMethods(declared))
            {
                if (methodInfo.Name == methodName)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Determines whether the ViewModel type defines no member matching the specified name, indicating a potential by-name dynamic binding candidate.
    /// </summary>
    /// <param name="viewModelType">The ViewModel type to inspect.</param>
    /// <param name="propertyName">The member name.</param>
    /// <returns><see langword="true"/> if no member exists or the type is <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Differentiates between missing members (which may exist on derived runtime instances and should emit by-name bindings)
    /// and non-public members (which fail in both compiled and XML modes).
    /// </remarks>
    public static bool DeclaresNoMember(Type? viewModelType, string propertyName)
    {
        if (viewModelType == null)
        {
            return true;
        }
        TypeDependencies.Inspect(viewModelType);
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var propertyInfo in viewModelType.GetProperties(all))
        {
            if (propertyInfo.Name == propertyName)
            {
                return false;
            }
        }
        return Resolver?.ResolveProperty(viewModelType, propertyName, out _) == null;
    }

    /// <summary>
    /// Resolves a command method on the specified ViewModel type or its registered mixins, giving precedence to mixin methods.
    /// </summary>
    /// <param name="viewModelType">The ViewModel type to inspect.</param>
    /// <param name="methodName">The command method name.</param>
    /// <param name="mixinType">Outputs the mixin type if the method was resolved on a mixin; otherwise, <see langword="null"/>.</param>
    /// <returns>The resolved <see cref="MethodInfo"/>, or <see langword="null"/> if not found.</returns>
    public static MethodInfo? GetMethod(Type? viewModelType, string methodName, out Type? mixinType)
    {
        mixinType = null;
        if (viewModelType == null)
        {
            return null;
        }
        TypeDependencies.Inspect(viewModelType);
        if (Resolver?.ResolveMethod(viewModelType, methodName, out mixinType) is { } fromMixin)
        {
            TypeDependencies.Inspect(mixinType);
            TypeDependencies.InspectOverloads(mixinType, methodName);
            return fromMixin;
        }
        mixinType = null;
        var method = viewModelType.GetMethod(methodName, MethodFlags);
        TypeDependencies.Inspect(method);
        TypeDependencies.InspectOverloads(viewModelType, methodName);
        return method;
    }

    /// <summary>
    /// Generates a C# source expression retrieving the mixin instance attached to the specified ViewModel instance expression.
    /// </summary>
    public static string GetMixinAccessExpression(Type mixinType, string viewModelExpression)
    {
        return $"global::Bannerlord.UIExtenderEx.ViewModels.ViewModelMixins.Get<{GetCodeTypeName(mixinType)}>({viewModelExpression})";
    }

    /// <summary>
    /// Formats a <see cref="Type"/> as a fully qualified C# type name rooted in <c>global::</c>, handling nested generic types, arrays, and pointers.
    /// </summary>
    /// <param name="type">The type to format.</param>
    /// <returns>A valid C# type name, or <see langword="null"/> if the type cannot be referenced directly (such as generic parameters or by-ref types).</returns>
    /// <remarks>
    /// Recursively formats generic type arguments across nested type hierarchies, partitioning arguments according to enclosing type arity.
    /// </remarks>
    public static string? GetCodeTypeName(Type type)
    {
        // Whatever the generated code names, it binds to
        TypeDependencies.Inspect(type);
        if (type.IsGenericParameter || type.IsByRef)
        {
            return null;
        }
        if (type.IsArray)
        {
            return GetCodeTypeName(type.GetElementType()!) is { } elementName
                ? $"{elementName}[{new string(',', type.GetArrayRank() - 1)}]"
                : null;
        }
        if (type.IsPointer)
        {
            return GetCodeTypeName(type.GetElementType()!) is { } elementName ? elementName + "*" : null;
        }
        var arguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
        return GetCodeTypeName(type, arguments, arguments.Length);
    }

    /// <summary>
    /// Formats a type name component recursively, distributing generic type arguments across enclosing types.
    /// </summary>
    private static string? GetCodeTypeName(Type type, Type[] arguments, int count)
    {
        var name = type.Name;
        var ownCount = 0;
        var tick = name.IndexOf('`');
        if (tick >= 0)
        {
            ownCount = int.Parse(name.Substring(tick + 1), System.Globalization.CultureInfo.InvariantCulture);
            name = name.Substring(0, tick);
        }

        string prefix;
        if (type.IsNested)
        {
            if (GetCodeTypeName(type.DeclaringType!, arguments, count - ownCount) is not { } declaringName)
            {
                return null;
            }
            prefix = declaringName + ".";
        }
        else
        {
            // Roots the namespace in global:: to avoid ambiguous type lookups across referenced assemblies.
            prefix = string.IsNullOrEmpty(type.Namespace) ? "global::" : "global::" + type.Namespace + ".";
        }

        if (ownCount == 0)
        {
            return prefix + name;
        }
        var ownArguments = new string[ownCount];
        for (var i = 0; i < ownCount; i++)
        {
            if (GetCodeTypeName(arguments[count - ownCount + i]) is not { } argumentName)
            {
                return null;
            }
            ownArguments[i] = argumentName;
        }
        return $"{prefix}{name}<{string.Join(", ", ownArguments)}>";
    }
}