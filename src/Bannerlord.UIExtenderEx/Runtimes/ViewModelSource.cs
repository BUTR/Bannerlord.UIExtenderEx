using Bannerlord.UIExtenderEx.Extensions;

using System.Reflection;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Runtimes;

/// <summary>
/// Resolves property and command members against a live <see cref="ViewModel"/> instance's dynamic binding table,
/// including attached mixin members.
/// </summary>
public static class ViewModelSource
{
    /// <summary>Resolves the <see cref="PropertyInfo"/> that <see cref="ViewModel.GetPropertyValue(string)"/> reads on the specified instance.</summary>
    public static PropertyInfo? SelectProperty(ViewModel viewModel, string name) => viewModel.SelectProperty(name);

    /// <summary>Resolves the <see cref="MethodInfo"/> that <see cref="ViewModel.ExecuteCommand"/> invokes on the specified instance.</summary>
    public static MethodInfo? SelectMethod(ViewModel viewModel, string name) => viewModel.SelectMethod(name);
}