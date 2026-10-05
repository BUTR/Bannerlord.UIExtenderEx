using System;

namespace Bannerlord.UIExtenderEx.Attributes;

/// <summary>
/// Specifies the target member kind accessed by a <see cref="BUTRUnsafeAccessorAttribute"/> method stub.
/// </summary>
public enum BUTRAccessorKind
{
    /// <summary>Targets an instance method. The stub's first parameter represents the target instance, followed by the target method's parameters.</summary>
    Method,

    /// <summary>Targets an instance field. The stub accepts the target instance and returns a reference (<see langword="ref"/>) to the field.</summary>
    Field,

    /// <summary>Targets a static method declared on <see cref="BUTRUnsafeAccessorAttribute.Type"/>. The stub parameters match the target method parameters.</summary>
    StaticMethod,

    /// <summary>Targets a static field declared on <see cref="BUTRUnsafeAccessorAttribute.Type"/>. The stub takes no parameters and returns a reference (<see langword="ref"/>) to the field.</summary>
    StaticField,
}

/// <summary>
/// Marks a static stub method to provide direct access to private or internal members across assembly boundaries.
/// <para>
/// When the declaring assembly is registered, UIExtenderEx transpiles the stub's body to emit direct access IL
/// bypassing runtime visibility checks. If the targeted member cannot be resolved, the stub retains its original
/// placeholder body and logs a diagnostic warning.
/// </para>
/// <para>
/// Stub methods must provide a placeholder body (such as <c>throw new NotImplementedException();</c>) and should be decorated
/// with <c>[MethodImpl(MethodImplOptions.NoInlining)]</c> to prevent call-site inlining prior to transpilation. Target resolution
/// matches <see cref="Name"/> (or the stub method name if omitted), parameter types, and return type.
/// </para>
/// <para>
/// Modeled after .NET 8's <c>System.Runtime.CompilerServices.UnsafeAccessorAttribute</c> for compatibility with .NET Framework 4.7.2
/// and .NET 6.0 runtimes.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_categories")]
/// [MethodImpl(MethodImplOptions.NoInlining)]
/// private static ref List&lt;ViewModel&gt; Categories(OptionsVM instance) => throw new NotImplementedException();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class BUTRUnsafeAccessorAttribute : Attribute
{
    /// <summary>Gets the kind of member accessed by this stub.</summary>
    public BUTRAccessorKind Kind { get; }

    /// <summary>
    /// Gets the target declaring type for static members. For instance members, the declaring type is inferred from
    /// the stub's first parameter.
    /// </summary>
    public Type? Type { get; }

    /// <summary>Gets or sets the target member name. When <see langword="null"/>, the stub method's own name is used.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="BUTRUnsafeAccessorAttribute"/> for an instance member.
    /// </summary>
    /// <param name="kind">The accessor kind (<see cref="BUTRAccessorKind.Method"/> or <see cref="BUTRAccessorKind.Field"/>).</param>
    public BUTRUnsafeAccessorAttribute(BUTRAccessorKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="BUTRUnsafeAccessorAttribute"/> for a static member on a specified type.
    /// </summary>
    /// <param name="kind">The accessor kind (<see cref="BUTRAccessorKind.StaticMethod"/> or <see cref="BUTRAccessorKind.StaticField"/>).</param>
    /// <param name="type">The type declaring the target static member.</param>
    public BUTRUnsafeAccessorAttribute(BUTRAccessorKind kind, Type type)
    {
        Kind = kind;
        Type = type;
    }
}