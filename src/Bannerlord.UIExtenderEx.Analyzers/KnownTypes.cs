using Microsoft.CodeAnalysis;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// The UIExtenderEx and game types the rules compare against, by the exact types the runtime checks for.
/// <c>DataSourceProperty</c> is the game's; <c>DataSourceMethodAttribute</c> is UIExtenderEx's own.
/// </summary>
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
    /// Whether the UIExtenderEx referenced is older than 3.0, whose registration fails where a newer one skips and says
    /// so: it throws on a refresh method it cannot find. The assembly version is the package version.
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

    /// <summary>The attributes a UIExtenderEx older than the analyzer does not have.</summary>
    private KnownTypes WithOptionalAttributes(Compilation compilation)
    {
        OverrideAttribute = compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Attributes.BUTRViewModelOverrideAttribute");
        UnsafeAccessorAttribute = compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Attributes.BUTRUnsafeAccessorAttribute");
        PrefabLinkAttribute = compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Attributes.PrefabLinkAttribute");
        return this;
    }

    /// <summary>Null when the compilation does not reference UIExtenderEx, which leaves nothing to check.</summary>
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
