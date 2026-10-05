using System;
using System.Collections.Generic;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

/// <summary>
/// Specifies the type of dynamic member access operation attempted, used for diagnostic reporting.
/// </summary>
public enum DynamicMemberOperation
{
    /// <summary>Indicates a dynamic property read operation.</summary>
    Read,
    /// <summary>Indicates a dynamic property write operation.</summary>
    Write,
    /// <summary>Indicates a dynamic command execution operation.</summary>
    Execute,
}

/// <summary>
/// Provides fallback runtime reflection and dynamic dispatch for compiled prefab databindings
/// when member names cannot be resolved against declared types at compile time.
/// <para>
/// Replicates the resolution behavior of the Gauntlet XML loader by querying the runtime ViewModel
/// property and command tables (including mixin-registered dynamic properties and commands). If a member query
/// returns null, consults <see cref="Host"/> to distinguish between legitimate null values and unmapped members,
/// emitting diagnostic warnings when unmapped members are encountered.
/// </para>
/// <para>
/// This dynamic pathway acts strictly as a fallback; statically resolvable bindings emit direct typed accessors
/// and bypass this mechanism entirely.
/// </para>
/// </summary>
public static class DynamicMember
{
    /// <summary>
    /// Tracks reported runtime types, member names, and operations to deduplicate diagnostic messages.
    /// </summary>
    private static readonly HashSet<(Type RuntimeType, string Name, DynamicMemberOperation Operation)> Reported = [];

    /// <summary>
    /// Synchronizes multi-threaded access to <see cref="Reported"/>.
    /// </summary>
    private static readonly object ReportLock = new();

    /// <summary>
    /// Gets or sets the dynamic member diagnostic host. When null, access behaves identically to the Gauntlet XML loader without diagnostic logging.
    /// </summary>
    public static IDynamicMemberHost? Host { get; set; }

    /// <summary>
    /// Clears the cache of reported diagnostic warnings. Intended for testing and benchmark resets.
    /// </summary>
    public static void Reset()
    {
        lock (ReportLock)
            Reported.Clear();
    }

    /// <summary>
    /// Retrieves the dynamic property value for <paramref name="name"/> on <paramref name="target"/>, querying <see cref="Host"/> if the result is null to detect missing properties.
    /// </summary>
    /// <param name="target">The target view model instance.</param>
    /// <param name="name">The property name to read.</param>
    /// <returns>The property value if found; otherwise, <see langword="null"/>.</returns>
    public static object? Get(ViewModel? target, string name)
    {
        if (target is null)
            return null;

        var value = target.GetPropertyValue(name);
        if (value is null && Host is { } host && !host.HasProperty(target, name))
            Report(host, target, name, DynamicMemberOperation.Read);
        return value;
    }

    /// <summary>
    /// Writes a value to the specified dynamic property by name, reporting a diagnostic warning if the property does not exist.
    /// </summary>
    /// <param name="target">The target view model instance.</param>
    /// <param name="name">The property name to assign.</param>
    /// <param name="value">The value to assign.</param>
    public static void Set(ViewModel? target, string name, object? value)
    {
        if (target is null)
            return;

        if (Host is { } host)
        {
            if (!host.TrySetProperty(target, name, value))
                Report(host, target, name, DynamicMemberOperation.Write);
            return;
        }

        target.SetPropertyValue(name, value);
    }

    /// <summary>
    /// Evaluates a child data source property, returning either a <see cref="ViewModel"/> or an <see cref="IMBBindingList"/>.
    /// </summary>
    /// <param name="target">The target view model instance.</param>
    /// <param name="name">The child property name to retrieve.</param>
    /// <returns>The child view model or binding list if found; otherwise, <see langword="null"/>.</returns>
    public static object? GetChild(ViewModel? target, string name) => Get(target, name) switch
    {
        ViewModel viewModel => viewModel,
        IMBBindingList bindingList => bindingList,
        _ => null,
    };

    /// <summary>
    /// Navigates a single segment of a data source path on <paramref name="owner"/>, resolving view model properties or binding list indices.
    /// </summary>
    /// <param name="owner">The current view model or binding list node in the path.</param>
    /// <param name="node">The property name or list index string for the path segment.</param>
    /// <returns>The resolved child data source if valid; otherwise, <see langword="null"/>.</returns>
    public static object? Step(object? owner, string node) => owner switch
    {
        ViewModel viewModel => GetChild(viewModel, node),
        IMBBindingList list => GetListItem(list, node),
        _ => null,
    };

    /// <summary>
    /// Filters an object value to ensure it is a valid Gauntlet data source (<see cref="ViewModel"/> or <see cref="IMBBindingList"/>).
    /// </summary>
    /// <param name="value">The candidate object to evaluate.</param>
    /// <returns>The value cast as an object if it is a view model or binding list; otherwise, <see langword="null"/>.</returns>
    public static object? AsDataSource(object? value) => value is ViewModel or IMBBindingList ? value : null;

    /// <summary>
    /// Retrieves a data source item at the specified string-encoded index from a binding list.
    /// </summary>
    /// <param name="list">The binding list to index into.</param>
    /// <param name="index">The zero-based index string to convert and retrieve.</param>
    /// <returns>The child view model or binding list at the index; otherwise, <see langword="null"/> if out of bounds or empty.</returns>
    public static object? GetListItem(IMBBindingList? list, string index)
    {
        if (list is null || list.Count <= 0)
            return null;
        var position = Convert.ToInt32(index);
        if (position < 0 || position >= list.Count)
            return null;
        return list[position] switch
        {
            ViewModel viewModel => viewModel,
            IMBBindingList bindingList => bindingList,
            _ => null,
        };
    }

    /// <summary>
    /// Executes a view model command by name with prepared arguments, verifying method existence via <see cref="Host"/> prior to execution.
    /// </summary>
    /// <param name="target">The target view model instance.</param>
    /// <param name="name">The command name to execute.</param>
    /// <param name="preparedArguments">The prepared argument array for the command invocation.</param>
    public static void Execute(ViewModel? target, string name, object[] preparedArguments)
    {
        if (target is null)
            return;

        if (Host is { } host && !host.HasMethod(target, name))
        {
            Report(host, target, name, DynamicMemberOperation.Execute);
            return;
        }

        target.ExecuteCommand(name, preparedArguments);
    }

    /// <summary>
    /// Logs a deduplicated diagnostic warning to <paramref name="host"/> when an unmapped dynamic member is accessed.
    /// </summary>
    /// <param name="host">The dynamic member host receiver.</param>
    /// <param name="target">The target view model instance where the member was missing.</param>
    /// <param name="name">The name of the missing member.</param>
    /// <param name="operation">The operation that triggered the lookup.</param>
    internal static void Report(IDynamicMemberHost host, ViewModel target, string name, DynamicMemberOperation operation)
    {
        var runtimeType = target.GetType();
        lock (ReportLock)
        {
            if (!Reported.Add((runtimeType, name, operation)))
                return;
        }
        host.Report($"UIExtenderEx: compiled prefab binding '{name}' ({operation}) found no member on {runtimeType.FullName}.");
    }
}