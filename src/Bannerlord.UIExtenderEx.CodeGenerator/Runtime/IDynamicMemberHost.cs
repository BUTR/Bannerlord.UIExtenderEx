using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

/// <summary>
/// Defines the host contract for resolving and diagnosing dynamic member queries on <see cref="ViewModel"/> instances.
/// <para>
/// Bridges compiled prefabs with the runtime UIExtenderEx dynamic property and mixin tables, enabling compiled code
/// to query mixin registrations and report unmapped member accesses without creating a direct cyclic compile-time reference.
/// </para>
/// </summary>
public interface IDynamicMemberHost
{
    /// <summary>
    /// Determines whether <paramref name="target"/> provides a property named <paramref name="name"/>, querying both native view model properties and mixin registrations.
    /// </summary>
    /// <param name="target">The view model instance to query.</param>
    /// <param name="name">The property name to inspect.</param>
    /// <returns><see langword="true"/> if the property exists on the target or its mixins; otherwise, <see langword="false"/>.</returns>
    bool HasProperty(ViewModel target, string name);

    /// <summary>
    /// Determines whether <paramref name="target"/> provides an executable command method named <paramref name="name"/>, including mixin-registered commands.
    /// </summary>
    /// <param name="target">The view model instance to query.</param>
    /// <param name="name">The command method name to inspect.</param>
    /// <returns><see langword="true"/> if the method exists on the target or its mixins; otherwise, <see langword="false"/>.</returns>
    bool HasMethod(ViewModel target, string name);

    /// <summary>
    /// Attempts to set a property on <paramref name="target"/> by name via its public setter or registered mixin setter.
    /// </summary>
    /// <param name="target">The view model instance to modify.</param>
    /// <param name="name">The property name to assign.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns><see langword="true"/> if the property exists; <see langword="false"/> if the property was not found in the binding table.</returns>
    bool TrySetProperty(ViewModel target, string name, object? value);

    /// <summary>
    /// Logs a deduplicated diagnostic warning message to the host environment.
    /// </summary>
    /// <param name="message">The formatted diagnostic message to log.</param>
    void Report(string message);
}