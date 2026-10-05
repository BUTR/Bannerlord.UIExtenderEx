using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

using System;
using System.Collections.Generic;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

/// <summary>
/// Encapsulates generation options and databinding contexts that differentiate compiled prefab class variants.
/// </summary>
/// <param name="DataSourceType">
/// The ViewModel type bound to the class, or <see langword="null"/> to generate an unbound widget tree.
/// </param>
internal sealed record PrefabClassOptions(Type? DataSourceType)
{
    /// <summary>Gets a value indicating whether the root widget suppresses automatic data source binding (see <see cref="WidgetBinding.IgnoresRootDataSource"/>).</summary>
    public bool IgnoresRootDataSource { get; init; }

    /// <summary>
    /// Gets the overriding DataSource path supplied by the parent widget for this prefab's root view.
    /// </summary>
    public string? RootDataSource { get; init; }

    /// <summary>
    /// Gets comma-separated command identifiers that must not be bound on the root widget because the parent widget overrides them.
    /// </summary>
    public string? OverriddenCommands { get; init; }

    /// <summary>Enumerates active option key-value pairs for diagnostic header comments.</summary>
    public IEnumerable<(string Name, object Value)> Describe()
    {
        if (DataSourceType is not null)
            yield return ("DataSourceType", DataSourceType);
        if (IgnoresRootDataSource)
            yield return ("IgnoresRootDataSource", true);
        if (RootDataSource is not null)
            yield return ("RootDataSource", RootDataSource);
        if (OverriddenCommands is not null)
            yield return ("OverriddenCommands", OverriddenCommands);
    }
}