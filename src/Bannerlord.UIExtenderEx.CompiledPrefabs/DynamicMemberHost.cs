using Bannerlord.BUTR.Shared.Utils;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.Runtimes;

using System.Diagnostics;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Resolves dynamic member and method bindings on <see cref="ViewModel"/> instances at runtime for compiled prefabs.
/// <para>
/// Bridges generated <see cref="DynamicMember"/> calls with <see cref="ViewModelSource"/>, enabling reflection-free access
/// to dynamic properties and methods contributed by view model mixins.
/// </para>
/// </summary>
public sealed class DynamicMemberHost : IDynamicMemberHost
{
    /// <summary>Attempts to resolve and assign a property on the target <see cref="ViewModel"/> or its registered mixins.</summary>
    public bool TrySetProperty(ViewModel target, string name, object? value)
    {
        var property = ViewModelSource.SelectProperty(target, name);
        if (property is null)
            return false;

        property.GetSetMethod()?.InvokeWithLog(target, value);
        return true;
    }

    /// <summary>Determines whether the specified property exists on the target <see cref="ViewModel"/> or its registered mixins.</summary>
    public bool HasProperty(ViewModel target, string name) => ViewModelSource.SelectProperty(target, name) is not null;

    /// <summary>Determines whether the specified method exists on the target <see cref="ViewModel"/> or its registered mixins.</summary>
    public bool HasMethod(ViewModel target, string name) => ViewModelSource.SelectMethod(target, name) is not null;

    /// <summary>Logs a diagnostic trace warning message.</summary>
    public void Report(string message) => Trace.TraceWarning(message);
}