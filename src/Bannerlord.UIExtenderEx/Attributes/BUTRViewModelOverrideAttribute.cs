using System;

namespace Bannerlord.UIExtenderEx.Attributes;

/// <summary>
/// Marks a mixin method to intercept and override an instance method on the extended host ViewModel.
/// <para>
/// The override method signature must accept all parameters of the target host method, followed by an additional
/// delegate parameter (<c>original</c>) matching the target method's parameter list. Calling <c>original</c> invokes
/// the next chained override in registration order or the original host implementation once the chain completes.
/// Omitting the invocation suppresses subsequent overrides and the original host method body.
/// </para>
/// <para>
/// Only instance methods returning <see langword="void"/> and containing no <see langword="ref"/> or <see langword="out"/>
/// parameters can be intercepted. Overrides apply across all caller invocations (Gauntlet prefab command bindings,
/// hotkeys, and direct engine calls) on ViewModel instances where the mixin is enabled.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [BUTRViewModelOverride(nameof(OptionsVM.ExecuteDone))]
/// private void ExecuteDone(Action original) => _modOptions.ExecuteDoneInternal(false, original);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class BUTRViewModelOverrideAttribute : Attribute
{
    /// <summary>Gets the name of the host ViewModel method intercepted by this override.</summary>
    public string MethodName { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BUTRViewModelOverrideAttribute"/> targeting the specified method.
    /// </summary>
    /// <param name="methodName">The name of the host ViewModel method to intercept.</param>
    public BUTRViewModelOverrideAttribute(string methodName)
    {
        MethodName = methodName;
    }
}