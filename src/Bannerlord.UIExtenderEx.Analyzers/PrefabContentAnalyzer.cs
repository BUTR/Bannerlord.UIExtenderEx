using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System.Collections.Immutable;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Checks the member that supplies an insert patch's content the way <c>PrefabComponent.TryGetNodes</c> finds and binds
/// it when the patch is registered: among the patch's public members, as a parameterless instance delegate returning the
/// type its attribute names, which a covariant return satisfies.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrefabContentAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.ContentMemberCannotSupplyContent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var contentAttribute = start.Compilation.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch+PrefabExtensionContentAttribute");
            var xmlNode = start.Compilation.GetTypeByMetadataName("System.Xml.XmlNode");
            var xmlDocument = start.Compilation.GetTypeByMetadataName("System.Xml.XmlDocument");
            if (contentAttribute is null || xmlNode is null || xmlDocument is null)
                return;
            var xmlNodes = start.Compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T).Construct(xmlNode);
            var @string = start.Compilation.GetSpecialType(SpecialType.System_String);
            var types = new ContentTypes(@string, xmlNode, xmlDocument, xmlNodes);
            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, contentAttribute, types), SymbolKind.Method, SymbolKind.Property);
        });
    }

    /// <summary>The types a content member can have, by the attribute on it.</summary>
    private sealed record ContentTypes(ITypeSymbol String, ITypeSymbol XmlNode, ITypeSymbol XmlDocument, ITypeSymbol XmlNodes);

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol contentAttribute, ContentTypes types)
    {
        var attribute = context.Symbol.GetAttributes().FirstOrDefault(a => DerivesFrom(a.AttributeClass, contentAttribute));
        if (attribute?.AttributeClass is null)
            return;

        // PrefabExtensionXmlDocument reads an XmlDocument, until the UIExtenderEx that made it obsolete (3.0) sends it the
        // XmlNode way
        ITypeSymbol? expected = attribute.AttributeClass.Name switch
        {
            "PrefabExtensionFileNameAttribute" or "PrefabExtensionTextAttribute" => types.String,
            "PrefabExtensionXmlNodeAttribute" => types.XmlNode,
            "PrefabExtensionXmlDocumentAttribute" => IsObsolete(attribute.AttributeClass) ? types.XmlNode : types.XmlDocument,
            "PrefabExtensionXmlNodesAttribute" => types.XmlNodes,
            _ => null,
        };
        if (expected is null)
            return;

        if (WhyNot(context.Symbol, attribute.AttributeClass, expected, context.Compilation) is var (why, reason))
        {
            var location = context.Symbol.Locations.FirstOrDefault() ?? Location.None;
            var fitting = reason == FixData.ReasonType ? FittingAttributes(context.Symbol, types.String, types.XmlNode, types.XmlNodes, context.Compilation) : [];
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.ContentMemberCannotSupplyContent, location,
                FixData.Of((FixData.Reason, reason), (FixData.Attributes, FixData.Join(fitting))), context.Symbol.Name, why));
        }
    }

    /// <summary>The content attributes whose type the member's is, for a member of the right shape under the wrong one.</summary>
    private static string[] FittingAttributes(ISymbol member, ITypeSymbol @string, ITypeSymbol xmlNode, ITypeSymbol xmlNodes, Compilation compilation)
    {
        var type = member switch
        {
            IPropertySymbol property => property.Type,
            IMethodSymbol method => method.ReturnType,
            _ => null,
        };
        if (type is null)
            return [];
        if (Fits(type, @string, compilation))
            return ["PrefabExtensionText", "PrefabExtensionFileName"];
        if (Fits(type, xmlNode, compilation))
            return ["PrefabExtensionXmlNode"];
        if (Fits(type, xmlNodes, compilation))
            return ["PrefabExtensionXmlNodes"];
        return [];
    }

    private static bool Fits(ITypeSymbol type, ITypeSymbol expected, Compilation compilation)
    {
        var conversion = compilation.ClassifyCommonConversion(type, expected);
        return conversion.IsIdentity || (conversion.IsImplicit && conversion.IsReference);
    }

    private static (string Why, string Reason)? WhyNot(ISymbol member, INamedTypeSymbol attribute, ITypeSymbol expected, Compilation compilation)
    {
        if (member.DeclaredAccessibility != Accessibility.Public)
            return ("it is not public, and the patch looks for its content among its public members", FixData.ReasonNotPublic);
        if (member.IsStatic)
            return ("it is static", FixData.ReasonStatic);

        ITypeSymbol type;
        switch (member)
        {
            case IPropertySymbol property:
                if (property.IsIndexer)
                    return ("an indexer takes parameters", "Indexer");
                if (property.GetMethod is null)
                    return ("it has no getter", "NoGetter");
                type = property.Type;
                break;
            case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                if (method.Parameters.Length > 0)
                    return ("it takes parameters", "Parameters");
                if (method.IsGenericMethod)
                    return ("it is generic", FixData.ReasonGeneric);
                type = method.ReturnType;
                break;
            default:
                return null;
        }

        if (Fits(type, expected, compilation))
            return null;

        var name = attribute.Name.Substring(0, attribute.Name.Length - "Attribute".Length);
        return ($"[{name}] needs {Display(expected)}, and it is {Display(type)}", FixData.ReasonType);
    }

    private static string Display(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);

    private static bool IsObsolete(INamedTypeSymbol attribute) =>
        attribute.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute");

    private static bool DerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }
        return false;
    }
}
