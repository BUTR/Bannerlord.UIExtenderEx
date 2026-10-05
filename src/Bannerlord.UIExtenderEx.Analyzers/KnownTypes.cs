using Microsoft.CodeAnalysis;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Caches well-known UIExtenderEx and TaleWorlds Gauntlet library type symbols resolved from the compilation context.
/// </summary>
/// <remarks>
/// Distinguishes engine-level attributes (such as TaleWorlds' <c>DataSourceProperty</c>) from UIExtenderEx extension
/// attributes (such as <c>DataSourceMethodAttribute</c>).
/// </remarks>
internal sealed class KnownTypes
{
    public INamedTypeSymbol ViewModelMixinAttribute { get; }
    public INamedTypeSymbol ViewModelMixinInterface { get; }
    public INamedTypeSymbol? BaseViewModelMixin { get; }
    public INamedTypeSymbol? DataSourcePropertyAttribute { get; }
    public INamedTypeSymbol? DataSourceMethodAttribute { get; }
    public INamedTypeSymbol? ViewModel { get; }
    public INamedTypeSymbol? OverrideAttribute { get; private set; }
    public INamedTypeSymbol? UnsafeAccessorAttribute { get; private set; }
    public INamedTypeSymbol? PrefabLinkAttribute { get; private set; }

    /// <summary>
    /// Indicates whether the referenced UIExtenderEx assembly precedes version 3.0 (where registration throws on missing refresh methods rather than logging warnings).
    /// </summary>
    public bool IsV2 => ViewModelMixinAttribute.ContainingAssembly.Identity.Version.Major < 3;

    private KnownTypes(INamedTypeSymbol viewModelMixinAttribute, INamedTypeSymbol viewModelMixinInterface, INamedTypeSymbol? baseViewModelMixin,
        INamedTypeSymbol? dataSourcePropertyAttribute, INamedTypeSymbol? dataSourceMethodAttribute, INamedTypeSymbol? viewModel)
    {
        ViewModelMixinAttribute = viewModelMixinAttribute;
        ViewModelMixinInterface = viewModelMixinInterface;
        BaseViewModelMixin = baseViewModelMixin;
        DataSourcePropertyAttribute = dataSourcePropertyAttribute;
        DataSourceMethodAttribute = dataSourceMethodAttribute;
        ViewModel = viewModel;
    }

    /// <summary>Attempts to resolve optional UIExtenderEx 3.0+ attributes when available in the compilation.</summary>
    private KnownTypes WithOptionalAttributes(Compilation compilation)
    {
        OverrideAttribute = compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Attributes.BUTRViewModelOverrideAttribute");
        UnsafeAccessorAttribute = compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Attributes.BUTRUnsafeAccessorAttribute");
        PrefabLinkAttribute = compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Attributes.PrefabLinkAttribute");
        return this;
    }

    /// <summary>
    /// Resolves required types from <paramref name="compilation"/>, returning <see langword="null"/> if UIExtenderEx is not referenced.
    /// </summary>
    public static KnownTypes? Create(Compilation compilation)
    {
        var attribute = compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Attributes.ViewModelMixinAttribute");
        var mixinInterface = compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.ViewModels.IViewModelMixin");
        if (attribute is null || mixinInterface is null)
            return null;

        return new KnownTypes(attribute, mixinInterface,
            compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.ViewModels.BaseViewModelMixin`1"),
            compilation.GetTypeByMetadataName("TaleWorlds.Library.DataSourceProperty"),
            compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Attributes.DataSourceMethodAttribute"),
            compilation.GetTypeByMetadataName("TaleWorlds.Library.ViewModel")).WithOptionalAttributes(compilation);
    }
}