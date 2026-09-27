using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

namespace Bannerlord.UIExtenderEx.Analyzers.Prefabs;

/// <summary>
/// A patch class: <c>[PrefabExtension(movie, xpath)]</c> on a <c>Prefabs2</c> insert or set-attribute patch, or on one of
/// the v1 <c>Prefabs</c> patches that go in at an XPath.
/// </summary>
internal sealed class PrefabPatch
{
    public INamedTypeSymbol Type { get; }
    public string Movie { get; }
    public string? XPath { get; }
    public Location XPathLocation { get; }
    public bool IsSetAttribute { get; }

    /// <summary>
    /// The <c>InsertType</c> member an insert patch's <c>Type</c> returns, when the build can read it. A v1 patch has it
    /// from its class: <c>Child</c> for an insert, <c>Replace</c>, <c>Prepend</c> or <c>Append</c>, <c>Custom</c> for a
    /// <c>CustomPatch&lt;XmlNode&gt;</c>.
    /// </summary>
    public string? InsertType { get; set; }

    public List<(PrefabXml Xml, bool RemoveRootNode)> Contents { get; } = [];
    public List<(string Name, string Value, Location NameLocation, Location ValueLocation)> SetAttributes { get; } = [];

    public PrefabPatch(INamedTypeSymbol type, string movie, string? xpath, Location xpathLocation, bool isSetAttribute)
    {
        Type = type;
        Movie = movie;
        XPath = xpath;
        XPathLocation = xpathLocation;
        IsSetAttribute = isSetAttribute;
    }
}

/// <summary>
/// Everything of the mod's that prefab XML is made of or reaches: its prefab files, the names they are registered under,
/// its patches and their content, and the movies it loads itself.
/// </summary>
internal sealed class PrefabSources
{
    private const string PatchesNamespace = "Bannerlord.UIExtenderEx.Prefabs2";
    private const string V1Namespace = "Bannerlord.UIExtenderEx.Prefabs";

    /// <summary>Every well-formed XML additional file, by file name without extension.</summary>
    public Dictionary<string, PrefabXml> FilesByName { get; } = new(StringComparer.Ordinal);

    /// <summary>The XML additional files that are not well-formed.</summary>
    public List<PrefabXml> MalformedFiles { get; } = [];

    /// <summary>The prefabs a tag can name: registered names, or a prefab file's own name when nothing registers it.</summary>
    public Dictionary<string, PrefabXml> PrefabsByTag { get; } = new(StringComparer.Ordinal);

    public List<PrefabPatch> Patches { get; } = [];

    /// <summary>
    /// The patches that may put into a movie XML the build cannot read: content built at runtime, a file it does not have,
    /// a <c>CustomPatch</c>. A node such a patch could insert is not reported missing for another patch.
    /// </summary>
    public List<(string Movie, INamedTypeSymbol Type)> UnreadChanges { get; } = [];

    /// <summary>Movies the mod loads itself with <c>LoadMovie("Name", viewModel)</c>, and the ViewModel's declared type.</summary>
    public List<(string Movie, INamedTypeSymbol ViewModel)> LoadedMovies { get; } = [];

    public static PrefabSources Collect(Compilation compilation, ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellation)
    {
        var sources = new PrefabSources();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in additionalFiles)
        {
            if (!file.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || !seen.Add(Path.GetFullPath(file.Path)))
                continue;
            if (file.GetText(cancellation) is not { } text)
                continue;
            var xml = PrefabXml.FromFile(file, text);
            if (xml.Document is null)
                sources.MalformedFiles.Add(xml);
            else
                sources.FilesByName[Path.GetFileNameWithoutExtension(file.Path)] = xml;
        }

        var registeredFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellation.ThrowIfCancellationRequested();
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot(cancellation);
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                sources.ReadInvocation(invocation, model, registeredFiles, cancellation);
            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                sources.ReadPatch(declaration, model, cancellation);
        }

        // A file no registration names is found by its own name, as a module's GUI/Prefabs folder is
        foreach (var pair in sources.FilesByName)
        {
            if (!registeredFiles.Contains(pair.Key) && IsPrefab(pair.Value) && !sources.PrefabsByTag.ContainsKey(pair.Key))
                sources.PrefabsByTag[pair.Key] = pair.Value;
        }
        return sources;
    }

    /// <summary>
    /// A prefab file as <c>WidgetPrefab.LoadFrom</c> takes one: rooted at <c>&lt;Prefab&gt;</c>, or at <c>&lt;Window&gt;</c> when
    /// it has no parameters, constants or visual definitions to declare.
    /// </summary>
    public static bool IsPrefab(PrefabXml xml) => xml.Document?.Root?.Name.LocalName is "Prefab" or "Window";

    /// <summary>
    /// <c>WidgetFactoryManager.CreateAndRegister("Name", Load("Mod.GUI.Prefabs.File.xml"))</c> and the like: the first
    /// argument names the prefab, a string ending in <c>.xml</c> among the rest names its file. And
    /// <c>LoadMovie("Name", viewModel)</c>.
    /// </summary>
    private void ReadInvocation(InvocationExpressionSyntax invocation, SemanticModel model, HashSet<string> registeredFiles, CancellationToken cancellation)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };
        if (name is not ("CreateAndRegister" or "Register" or "LoadMovie") || invocation.ArgumentList.Arguments.Count < 2)
            return;
        if (model.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression, cancellation).Value is not string first)
            return;

        if (name == "LoadMovie")
        {
            foreach (var argument in invocation.ArgumentList.Arguments.Skip(1))
            {
                if (model.GetTypeInfo(argument.Expression, cancellation).Type is INamedTypeSymbol type && IsViewModel(type))
                {
                    LoadedMovies.Add((first, type));
                    break;
                }
            }
            return;
        }

        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol { ContainingType.Name: "WidgetFactoryManager" })
            return;
        foreach (var literal in invocation.ArgumentList.Arguments.Skip(1).SelectMany(a => a.DescendantNodesAndSelf()).OfType<LiteralExpressionSyntax>())
        {
            if (literal.Token.Value is not string resource || !resource.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                continue;
            var fileName = FileNameOf(resource);
            if (!FilesByName.TryGetValue(fileName, out var file))
                continue;
            registeredFiles.Add(fileName);
            PrefabsByTag[first] = file;
            break;
        }
    }

    /// <summary>A resource name (<c>Mod.GUI.Prefabs.File.xml</c>) or a path (<c>GUI/Prefabs/File.xml</c>), down to <c>File</c>.</summary>
    private static string FileNameOf(string resource)
    {
        var withoutExtension = resource.Substring(0, resource.Length - ".xml".Length);
        var cut = Math.Max(withoutExtension.LastIndexOf('.'), Math.Max(withoutExtension.LastIndexOf('/'), withoutExtension.LastIndexOf('\\')));
        return cut >= 0 ? withoutExtension.Substring(cut + 1) : withoutExtension;
    }

    /// <summary>The patch classes <c>UIExtenderRuntime.Register</c> tells apart, in its order.</summary>
    private enum PatchKind
    {
        None,
        V1SetAttribute,
        V1Insert,
        V1Replace,
        V1InsertAsSibling,
        V1CustomDocument,
        V1CustomNode,
        SetAttribute,
        Insert,
    }

    private static PatchKind KindOf(INamedTypeSymbol type)
    {
        if (DerivesFrom(type, V1Namespace + ".PrefabExtensionSetAttributePatch"))
            return PatchKind.V1SetAttribute;
        if (DerivesFrom(type, V1Namespace + ".PrefabExtensionInsertPatch"))
            return PatchKind.V1Insert;
        if (DerivesFrom(type, V1Namespace + ".PrefabExtensionReplacePatch"))
            return PatchKind.V1Replace;
        if (DerivesFrom(type, V1Namespace + ".PrefabExtensionInsertAsSiblingPatch"))
            return PatchKind.V1InsertAsSibling;
        if (DerivesFrom(type, V1Namespace + ".CustomPatch<System.Xml.XmlDocument>"))
            return PatchKind.V1CustomDocument;
        if (DerivesFrom(type, V1Namespace + ".CustomPatch<System.Xml.XmlNode>"))
            return PatchKind.V1CustomNode;
        if (DerivesFrom(type, PatchesNamespace + ".PrefabExtensionSetAttributePatch"))
            return PatchKind.SetAttribute;
        if (DerivesFrom(type, PatchesNamespace + ".PrefabExtensionInsertPatch"))
            return PatchKind.Insert;
        return PatchKind.None;
    }

    private void ReadPatch(ClassDeclarationSyntax declaration, SemanticModel model, CancellationToken cancellation)
    {
        if (model.GetDeclaredSymbol(declaration, cancellation) is not INamedTypeSymbol type)
            return;
        var kind = KindOf(type);
        if (kind == PatchKind.None)
            return;

        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "Bannerlord.UIExtenderEx.Attributes.PrefabExtensionAttribute")
                continue;
            if (attribute.ConstructorArguments.FirstOrDefault().Value is not string movie)
                continue;

            // Handed the whole document, and not the XPath: nothing to check, but it changes the movie
            if (kind == PatchKind.V1CustomDocument)
            {
                UnreadChanges.Add((movie, type));
                continue;
            }

            var xpath = attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value as string : null;
            var syntax = attribute.ApplicationSyntaxReference?.GetSyntax(cancellation) as AttributeSyntax;
            var xpathLocation = syntax?.ArgumentList is { Arguments.Count: > 1 } arguments ? arguments.Arguments[1].GetLocation() : syntax?.GetLocation() ?? Location.None;
            var patch = new PrefabPatch(type, movie, xpath, xpathLocation, kind is PatchKind.SetAttribute or PatchKind.V1SetAttribute);
            switch (kind)
            {
                case PatchKind.Insert:
                    patch.InsertType = ReadInsertType(declaration, model, cancellation);
                    ReadInsertContent(patch, declaration, model, cancellation);
                    if (patch.Contents.Count == 0 && patch.InsertType != "Remove")
                        UnreadChanges.Add((movie, type));
                    break;
                case PatchKind.SetAttribute:
                    ReadSetAttributes(patch, declaration, model, cancellation);
                    break;
                case PatchKind.V1SetAttribute:
                    ReadV1SetAttribute(patch, declaration, cancellation);
                    break;
                case PatchKind.V1CustomNode:
                    patch.InsertType = "Custom";
                    UnreadChanges.Add((movie, type));
                    break;
                default:
                    patch.InsertType = kind switch
                    {
                        PatchKind.V1Insert => "Child",
                        PatchKind.V1Replace => "Replace",
                        _ => ReadInsertType(declaration, model, cancellation) ?? "Append",
                    };
                    ReadV1Content(patch, type, declaration, model, cancellation);
                    if (patch.Contents.Count == 0)
                        UnreadChanges.Add((movie, type));
                    break;
            }
            Patches.Add(patch);
        }
    }

    /// <summary>
    /// A v1 patch's content: <c>GetPrefabExtension()</c> returns an <c>XmlDocument</c>, which the build reads when the class
    /// hands <c>LoadXml</c> a literal, or when it is a <c>ModulePrefabExtensionInsertPatch</c> or
    /// <c>EmbedPrefabExtensionInsertPatch</c> naming a file of the mod. The document element is what goes in.
    /// </summary>
    private void ReadV1Content(PrefabPatch patch, INamedTypeSymbol type, ClassDeclarationSyntax declaration, SemanticModel model, CancellationToken cancellation)
    {
        // base("Name", "Module") loads Modules/Module/GUI/PrefabExtensions/Name.xml, base(assembly, "Mod.File.xml") a resource
        var fileArgument = DerivesFrom(type, V1Namespace + ".ModulePrefabExtensionInsertPatch") ? 0
            : DerivesFrom(type, V1Namespace + ".EmbedPrefabExtensionInsertPatch") ? 1
            : -1;
        if (fileArgument < 0)
        {
            ReadLoadXmlLiterals(patch, declaration, false);
            return;
        }

        foreach (var initializer in declaration.DescendantNodes().OfType<ConstructorInitializerSyntax>())
        {
            if (!initializer.IsKind(SyntaxKind.BaseConstructorInitializer) || initializer.ArgumentList.Arguments.Count <= fileArgument)
                continue;
            if (model.GetConstantValue(initializer.ArgumentList.Arguments[fileArgument].Expression, cancellation).Value is not string name)
                continue;
            var fileName = name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? FileNameOf(name) : name;
            if (FilesByName.TryGetValue(fileName, out var file))
            {
                patch.Contents.Add((file, false));
                return;
            }
        }
    }

    /// <summary>A v1 set-attribute patch: its <c>Attribute</c> and <c>Value</c> properties, each returning a literal.</summary>
    private static void ReadV1SetAttribute(PrefabPatch patch, ClassDeclarationSyntax declaration, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (ReturnedExpression(declaration, "Attribute") is LiteralExpressionSyntax { Token.Value: string name } nameLiteral
            && ReturnedExpression(declaration, "Value") is LiteralExpressionSyntax { Token.Value: string value } valueLiteral)
        {
            patch.SetAttributes.Add((name, value, nameLiteral.GetLocation(), valueLiteral.GetLocation()));
        }
    }

    /// <summary>What a property of the class returns, as <c>=&gt; value</c> or a getter of one return statement writes it.</summary>
    private static ExpressionSyntax? ReturnedExpression(ClassDeclarationSyntax declaration, string name)
    {
        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (property.Identifier.ValueText != name)
                continue;
            return property.ExpressionBody?.Expression
                   ?? property.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) switch
                   {
                       { ExpressionBody.Expression: { } body } => body,
                       { Body.Statements: { Count: 1 } statements } when statements[0] is ReturnStatementSyntax { Expression: { } returned } => returned,
                       _ => null,
                   };
        }
        return null;
    }

    /// <summary>
    /// The content of an insert patch, by the attribute on the member that supplies it: a file by name, text returned as
    /// a literal, or the literals the class hands to <c>LoadXml</c> for an <c>XmlNode</c> or <c>XmlDocument</c>.
    /// </summary>
    private void ReadInsertContent(PrefabPatch patch, ClassDeclarationSyntax declaration, SemanticModel model, CancellationToken cancellation)
    {
        foreach (var member in declaration.Members)
        {
            var symbol = model.GetDeclaredSymbol(member, cancellation);
            var content = symbol?.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ContainingType?.Name == "PrefabExtensionInsertPatch");
            if (content?.AttributeClass is null)
                continue;
            var removeRootNode = content.ConstructorArguments.FirstOrDefault().Value is true;
            var literals = member.DescendantNodes().OfType<LiteralExpressionSyntax>().Where(l => l.IsKind(SyntaxKind.StringLiteralExpression)).ToList();

            switch (content.AttributeClass.Name)
            {
                case "PrefabExtensionFileNameAttribute":
                    if (literals.FirstOrDefault()?.Token.Value is string fileName && FilesByName.TryGetValue(FileNameOf(fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".xml"), out var file))
                        patch.Contents.Add((file, removeRootNode));
                    break;
                case "PrefabExtensionTextAttribute":
                    if (literals.FirstOrDefault() is { } text)
                        patch.Contents.Add((PrefabXml.FromLiteral(text.Token), removeRootNode));
                    break;
                default:
                    ReadLoadXmlLiterals(patch, declaration, removeRootNode);
                    break;
            }
        }
    }

    /// <summary>The literals the class hands to <c>LoadXml</c>.</summary>
    private static void ReadLoadXmlLiterals(PrefabPatch patch, ClassDeclarationSyntax declaration, bool removeRootNode)
    {
        foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "LoadXml" }
                && invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                patch.Contents.Add((PrefabXml.FromLiteral(literal.Token), removeRootNode));
            }
        }
    }

    /// <summary>The member of <c>InsertType</c> the patch's <c>Type</c> property returns, as <c>=&gt; InsertType.Child</c> writes it.</summary>
    private static string? ReadInsertType(ClassDeclarationSyntax declaration, SemanticModel model, CancellationToken cancellation) =>
        ReturnedExpression(declaration, "Type") is { } expression
        && model.GetSymbolInfo(expression, cancellation).Symbol is IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } member
            ? member.Name
            : null;

    /// <summary><c>new Attribute("Name", "Value")</c> in a set-attribute patch.</summary>
    private static void ReadSetAttributes(PrefabPatch patch, ClassDeclarationSyntax declaration, SemanticModel model, CancellationToken cancellation)
    {
        foreach (var creation in declaration.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            if (creation.ArgumentList?.Arguments.Count != 2)
                continue;
            if (model.GetTypeInfo(creation, cancellation).Type is not { Name: "Attribute", ContainingType.Name: "PrefabExtensionSetAttributePatch" })
                continue;
            if (creation.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax { Token.Value: string name } nameLiteral
                || creation.ArgumentList.Arguments[1].Expression is not LiteralExpressionSyntax { Token.Value: string value } valueLiteral)
                continue;
            patch.SetAttributes.Add((name, value, nameLiteral.GetLocation(), valueLiteral.GetLocation()));
        }
    }

    private static bool DerivesFrom(INamedTypeSymbol type, string metadataName)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == metadataName)
                return true;
        }
        return false;
    }

    private static bool IsViewModel(ITypeSymbol type)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "TaleWorlds.Library.ViewModel")
                return true;
        }
        return false;
    }
}
