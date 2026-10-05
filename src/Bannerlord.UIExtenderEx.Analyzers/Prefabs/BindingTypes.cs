using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;

namespace Bannerlord.UIExtenderEx.Analyzers.Prefabs;

/// <summary>
/// Evaluates type compatibility across Gauntlet's two-way data-binding reflection pathways:
/// <list type="bullet">
/// <item><term>ViewModel to widget:</term> <description><c>GauntletView.RefreshBinding</c> passes the ViewModel property value to
/// <c>WidgetExtensions.SetWidgetAttribute</c>, which converts strings using <c>ConvertObject</c> (based on <c>stringConversions</c>
/// in <c>types.json</c>) before invoking the widget property setter.</description></item>
/// <item><term>Widget to ViewModel:</term> <description><c>GauntletView.OnViewPropertyChanged</c> transfers the widget property value
/// to <c>ViewModel.SetPropertyValue</c>, invoking the public setter without conversion.</description></item>
/// </list>
/// Compatible assignments require matching or derived reference types, unboxed primitives/enums conforming to widening conversions,
/// or nullable-wrapped counterparts. Incompatible types cause runtime <see cref="ArgumentException"/> failures in <see cref="System.Reflection.MethodInfo.Invoke"/>.
/// </summary>
internal static class BindingTypes
{
    private static readonly SymbolDisplayFormat WithoutNullableReferences =
        SymbolDisplayFormat.CSharpErrorMessageFormat.RemoveMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Determines whether a value of runtime type <paramref name="argument"/> can be assigned to a reflection parameter of type <paramref name="parameter"/> via <see cref="System.Reflection.MethodInfo.Invoke"/>.</summary>
    public static bool InvokeTakes(ITypeSymbol argument, ITypeSymbol parameter)
    {
        if (Unknowable(parameter) || argument.TypeKind is TypeKind.Error or TypeKind.TypeParameter)
            return true;
        // Matches target type or base class by name, ignoring nullable reference annotations (which reflection disregards at runtime).
        var name = parameter.ToDisplayString(WithoutNullableReferences);
        for (var type = argument; type is not null; type = type.BaseType)
        {
            if (type.ToDisplayString(WithoutNullableReferences) == name)
                return true;
        }
        if (parameter is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } nullable)
            return InvokeTakes(argument, nullable.TypeArguments[0]);
        // Evaluates primitive and enum widening conversions (enums are evaluated via their underlying numeric types).
        return Primitive(argument) is { } from && Primitive(parameter) is { } to && (from == to || Widens(from, to));
    }

    /// <summary>
    /// Determines whether the Gauntlet loader can pass a value of the declared ViewModel property type to the target widget property.
    /// Polymorphic types (<see cref="object"/>, interfaces, unsealed base classes) are permitted.
    /// Returns <see langword="null"/> if the source is a <see cref="string"/> that may require conversion via <c>ConvertObject</c>
    /// according to the game version's GUI package metadata (<c>GameConfiguration.StringConversions</c> or <see cref="FallbackStringConversions"/>).
    /// </summary>
    public static bool? LoaderHandsWidget(ITypeSymbol viewModelType, ITypeSymbol widgetType)
    {
        if (Unknowable(viewModelType) || Unknowable(widgetType))
            return true;
        if (viewModelType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } nullable)
            return LoaderHandsWidget(nullable.TypeArguments[0], widgetType);
        if (InvokeTakes(viewModelType, widgetType))
            return true;
        // An unsealed class instance may be a derived subtype compatible with the widget property.
        if (viewModelType is INamedTypeSymbol { TypeKind: TypeKind.Class, IsSealed: false } declared
            && (widgetType.TypeKind == TypeKind.Interface || InvokeTakes(widgetType, declared)))
            return true;
        // ConvertObject checks exact types; defer string compatibility evaluation to version-specific conversion sets.
        if (viewModelType.SpecialType == SpecialType.System_String)
            return null;
        return false;
    }

    /// <summary>
    /// Fallback set of metadata type names supported by <c>ConvertObject</c> string conversions when no game GUI bundle
    /// is provided or the package metadata omits <c>stringConversions</c>.
    /// </summary>
    public static readonly IReadOnlyCollection<string> FallbackStringConversions = new HashSet<string>(StringComparer.Ordinal)
    {
        "System.Int32", "TaleWorlds.TwoDimension.Sprite", "TaleWorlds.GauntletUI.Brush", "TaleWorlds.Library.Color",
    };

    /// <summary>Identifies types whose runtime values cannot be statically determined or restricted (e.g., <see cref="object"/>, interfaces, type parameters).</summary>
    internal static bool Unknowable(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum
        || type.TypeKind is TypeKind.Interface or TypeKind.TypeParameter or TypeKind.Error or TypeKind.Dynamic;

    private static SpecialType? Primitive(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying })
            return underlying.SpecialType;
        return type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_Double ? type.SpecialType : null;
    }

    /// <summary>Defines primitive widening conversions supported by <see cref="System.Reflection.MethodInfo.Invoke"/> when passing boxed arguments.</summary>
    private static readonly Dictionary<SpecialType, SpecialType[]> Widenings = new()
    {
        [SpecialType.System_Byte] = [SpecialType.System_Char, SpecialType.System_UInt16, SpecialType.System_Int16, SpecialType.System_UInt32, SpecialType.System_Int32, SpecialType.System_UInt64, SpecialType.System_Int64, SpecialType.System_Single, SpecialType.System_Double],
        [SpecialType.System_SByte] = [SpecialType.System_Int16, SpecialType.System_Int32, SpecialType.System_Int64, SpecialType.System_Single, SpecialType.System_Double],
        [SpecialType.System_Char] = [SpecialType.System_UInt16, SpecialType.System_UInt32, SpecialType.System_Int32, SpecialType.System_UInt64, SpecialType.System_Int64, SpecialType.System_Single, SpecialType.System_Double],
        [SpecialType.System_Int16] = [SpecialType.System_Int32, SpecialType.System_Int64, SpecialType.System_Single, SpecialType.System_Double],
        [SpecialType.System_UInt16] = [SpecialType.System_Char, SpecialType.System_UInt32, SpecialType.System_Int32, SpecialType.System_UInt64, SpecialType.System_Int64, SpecialType.System_Single, SpecialType.System_Double],
        [SpecialType.System_Int32] = [SpecialType.System_Int64, SpecialType.System_Single, SpecialType.System_Double],
        [SpecialType.System_UInt32] = [SpecialType.System_UInt64, SpecialType.System_Int64, SpecialType.System_Single, SpecialType.System_Double],
        [SpecialType.System_Int64] = [SpecialType.System_Single, SpecialType.System_Double],
        [SpecialType.System_UInt64] = [SpecialType.System_Single, SpecialType.System_Double],
        [SpecialType.System_Single] = [SpecialType.System_Double],
    };

    private static bool Widens(SpecialType from, SpecialType to) =>
        Widenings.TryGetValue(from, out var targets) && System.Array.IndexOf(targets, to) >= 0;
}