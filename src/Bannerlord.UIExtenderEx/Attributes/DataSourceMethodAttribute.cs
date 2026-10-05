using System;

namespace Bannerlord.UIExtenderEx.Attributes;

/// <summary>
/// Marks a public mixin method as an invokable Gauntlet command method exposed in the host ViewModel's dynamic binding table.
/// <para>
/// Equivalent to TaleWorlds' <c>[DataSourceProperty]</c> attribute for properties, registering the method to handle Gauntlet UI button and command bindings.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DataSourceMethodAttribute : Attribute;