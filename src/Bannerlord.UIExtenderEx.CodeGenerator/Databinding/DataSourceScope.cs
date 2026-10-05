using System;
using System.Collections.Generic;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Represents a discrete data source scope within a generated class, managing its field storage, path resolution,
/// bound widgets, and child scope hierarchy.
/// </summary>
internal sealed class DataSourceScope
{
    private string? _fieldName;

    private BindingPathResolution? _resolution;

    public DataSourceScope(BindingPath path)
    {
        Path = path;
    }

    /// <summary>Gets the binding path defining this scope.</summary>
    public BindingPath Path { get; }

    /// <summary>Gets a value indicating whether this scope represents the root data source ("Root").</summary>
    public bool IsRoot => Path.Path == "Root";

    /// <summary>Gets a value indicating whether this scope represents an outer path located above the class root.</summary>
    public bool IsOuter => OuterPath.IsOuter(Path);

    public List<DataSourceScope> Children { get; } = [];

    /// <summary>Gets the list of child prefab handoffs receiving this scope's data source as an outer path.</summary>
    public List<OuterHandoff> OuterHandoffs { get; } = [];

    /// <summary>Gets the list of widget bindings attached directly at this scope path.</summary>
    public List<WidgetBinding> Widgets { get; } = [];

    /// <summary>Gets the list of widget bindings passing this scope's data source into child prefabs.</summary>
    public List<WidgetBinding> PrefabHandoffs { get; } = [];

    /// <summary>Gets or sets the emitted private field name storing this scope's data source instance.</summary>
    public string FieldName
    {
        get => _fieldName ?? throw new InvalidOperationException($"The data source path '{Path.Path}' has no field yet.");
        set => _fieldName = value;
    }

    /// <summary>Gets or sets the type resolution decision for this scope.</summary>
    public BindingPathResolution Resolution
    {
        get => _resolution ?? throw new InvalidOperationException($"The data source path '{Path.Path}' was not resolved before code generation.");
        set => _resolution = value;
    }

    /// <summary>Gets a value indicating whether this scope represents a collection binding list.</summary>
    public bool IsList => Resolution.IsList;

    /// <summary>Gets or sets the optional field name storing the raw object instance for polymorphic scopes.</summary>
    public string? ObjectFieldName { get; set; }

    /// <summary>Gets the C# field access expression for the data source instance.</summary>
    public string ObjectAccess => ObjectFieldName ?? FieldName;
}