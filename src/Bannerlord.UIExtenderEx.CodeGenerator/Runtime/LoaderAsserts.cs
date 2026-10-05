using System;
using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

/// <summary>
/// Emits Gauntlet XML loader-compatible assertion failures when setting widget attributes fails in generated code.
/// <para>
/// Mirrors the exact diagnostic format and phrasing produced by <c>WidgetExtensions.SetWidgetAttributeFromString</c>
/// to ensure compiled prefabs produce identical diagnostic messages to TaleWorlds XML loader errors.
/// </para>
/// </summary>
public static class LoaderAsserts
{
    /// <summary>
    /// Gets the default failure message generated when invoking a property setter via reflection throws an exception.
    /// </summary>
    public static readonly string SetterFailure = new TargetInvocationException(null).Message;

    /// <summary>
    /// Emits a failed assertion matching Gauntlet's string-to-attribute conversion failure format.
    /// </summary>
    /// <param name="target">The target widget instance.</param>
    /// <param name="name">The attribute name that failed to assign.</param>
    /// <param name="value">The string value that could not be assigned.</param>
    /// <param name="failure">The failure reason or exception message.</param>
    public static void AttributeNotSet(object? target, string name, string? value, string failure) =>
        Debug.FailedAssert("Failed to set attribute from string.\nTarget:" + target + "\nName:" + name + "\nValue:" + value + "\n" + failure);

    /// <summary>
    /// Emits a failed assertion when a widget attribute setter throws an exception.
    /// </summary>
    /// <param name="target">The target widget instance.</param>
    /// <param name="name">The attribute name that threw.</param>
    /// <param name="value">The string value being assigned.</param>
    public static void SetterThrew(object? target, string name, string value) =>
        AttributeNotSet(target, name, value, SetterFailure);

    /// <summary>
    /// Emits a failed assertion when navigating a dotted widget property path or invoking its setter throws an exception.
    /// </summary>
    /// <param name="target">The target widget instance.</param>
    /// <param name="name">The attribute path that threw.</param>
    /// <param name="value">The string value being assigned.</param>
    /// <param name="failure">The caught exception encountered during path traversal or assignment.</param>
    public static void PathOrSetterThrew(object? target, string name, string value, Exception failure) =>
        AttributeNotSet(target, name, value, failure is NullReferenceException ? failure.Message : SetterFailure);
}