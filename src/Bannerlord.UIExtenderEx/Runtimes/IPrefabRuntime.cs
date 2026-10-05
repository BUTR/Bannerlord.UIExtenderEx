using System.Reflection;

using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Runtimes;

/// <summary>
/// Defines a movie loading strategy queried by UIExtenderEx prior to every UI movie load in <c>GauntletMovie.Load</c>.
/// <para>
/// Intended for runtime implementations rather than mod consumers. While mod extensions (prefab patches and ViewModel mixins)
/// determine visual and logical structure across all runtimes, the active runtime controls execution strategy and performance
/// without altering rendered content.
/// </para>
/// </summary>
public interface IPrefabRuntime
{
    /// <summary>
    /// Attempts to serve a movie via a pre-compiled or dynamically compiled widget variant registered for the specified
    /// <c>(movie, dataSource)</c> pair in <see cref="WidgetFactory.GeneratedPrefabContext"/>.
    /// </summary>
    /// <param name="widgetFactory">The widget factory requesting the movie.</param>
    /// <param name="movieName">The name of the movie to load.</param>
    /// <param name="dataSource">The active data source or ViewModel instance.</param>
    /// <returns>
    /// <see langword="true"/> if this runtime vouches for and activates an eligible registered variant; otherwise,
    /// <see langword="false"/> to delegate to the next registered runtime or fall back to the XML loader.
    /// </returns>
    bool TryServe(WidgetFactory widgetFactory, string movieName, IViewModel? dataSource);

    /// <summary>
    /// Determines whether a compiled prefab assembly registered in <see cref="GeneratedPrefabContext"/> originated from this runtime implementation.
    /// </summary>
    /// <param name="variantAssembly">The compiled variant assembly to check.</param>
    /// <returns><see langword="true"/> if the assembly was generated and registered by this runtime; otherwise, <see langword="false"/>.</returns>
    bool IsOwnVariant(Assembly variantAssembly);
}