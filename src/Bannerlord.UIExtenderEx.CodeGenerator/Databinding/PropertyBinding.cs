using System;
using System.Reflection;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Encapsulates a two-way <c>Property="@Path"</c> databinding linking a widget property and a view model property.
/// </summary>
internal sealed class PropertyBinding
{
    public PropertyBinding(string property, string path)
    {
        Property = property;
        Path = path;
    }

    /// <summary>Gets the widget property name targeted by this binding.</summary>
    public string Property { get; }

    /// <summary>Gets the view model property navigation path relative to the active data source.</summary>
    public string Path { get; }

    /// <summary>Gets or sets the resolved widget property reflection metadata.</summary>
    public PropertyInfo? WidgetProperty { get; set; }

    /// <summary>Gets the CLR type of the target widget property.</summary>
    public Type? WidgetPropertyType => WidgetProperty?.PropertyType;

    /// <summary>Gets or sets the CLR type of the bound view model property.</summary>
    public Type? ViewModelPropertyType { get; set; }

    /// <summary>Gets a value indicating whether value assignment requires type conversion between view model and widget.</summary>
    public bool RequiresConversion => ViewModelPropertyType is { } viewModelType && WidgetPropertyType is { } widgetType && !widgetType.IsAssignableFrom(viewModelType);

    /// <summary>Gets or sets the mixin type providing the bound view model property, if declared on a mixin.</summary>
    public Type? MixinType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether property access falls back to the view model if the mixin is absent.
    /// </summary>
    public bool FallsBackToViewModel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the property must be bound dynamically by name against runtime instances.
    /// </summary>
    public bool BindsByName { get; set; }
}