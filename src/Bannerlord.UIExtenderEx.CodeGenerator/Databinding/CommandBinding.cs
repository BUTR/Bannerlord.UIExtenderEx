using System;
using System.Linq;
using System.Reflection;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Databinding;

/// <summary>
/// Encapsulates a <c>Command.X="Path"</c> event-to-method binding on a widget node.
/// </summary>
internal sealed class CommandBinding
{
    private MethodInfo? _method;

    public CommandBinding(string command, string path)
    {
        Command = command;
        Path = path;
    }

    /// <summary>Gets the widget event name that triggers the command.</summary>
    public string Command { get; }

    /// <summary>Gets the relative navigation path to the command method from the widget data source.</summary>
    public string Path { get; }

    /// <summary>Gets or sets the statically resolved target method, or <see langword="null"/> if dispatched dynamically by name.</summary>
    public MethodInfo? Method
    {
        get => _method;
        set
        {
            _method = value;
            ParameterTypes = value?.GetParameters().Select(x => x.ParameterType).ToArray() ?? Type.EmptyTypes;
        }
    }

    /// <summary>Gets the parameter types accepted by the target command method.</summary>
    public Type[] ParameterTypes { get; private set; } = Type.EmptyTypes;

    /// <summary>Gets or sets the constant parameter argument passed to the command via <c>CommandParameter.X</c>.</summary>
    public string? Parameter { get; set; }

    /// <summary>Gets or sets the mixin type declaring the command method, if provided by an attached mixin.</summary>
    public Type? MixinType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether command execution falls back to the view model method if the mixin is absent.
    /// </summary>
    public bool FallsBackToViewModel { get; set; }
}