using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Resolves each <c>[BUTRUnsafeAccessor]</c> stub the way <c>UnsafeAccessorPatch.Resolve</c> does when the assembly is
/// registered, so a stub naming a member the game no longer has is found while building against that game version, not
/// at start-up.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsafeAccessorAnalyzer : DiagnosticAnalyzer
{
    // BUTRAccessorKind
    private const int Method = 0, Field = 1, StaticMethod = 2, StaticField = 3;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.AccessorNotResolved, Descriptors.AccessorInlinable);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (KnownTypes.Create(start.Compilation) is not { UnsafeAccessorAttribute: { } attribute })
                return;
            var hosts = new Hosts(start.Compilation);
            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, attribute, hosts), SymbolKind.Method);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attributeType, Hosts hosts)
    {
        var stub = (IMethodSymbol) context.Symbol;
        var attribute = stub.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
        if (attribute is null)
            return;

        var location = stub.Locations.FirstOrDefault() ?? Location.None;
        if (WhyNotResolved(stub, attribute, hosts) is { } why)
        {
            context.Report(Diagnostic.Create(Descriptors.AccessorNotResolved, location, stub.Name, why));
            return;
        }
        if ((stub.MethodImplementationFlags & MethodImplAttributes.NoInlining) == 0)
            context.Report(Diagnostic.Create(Descriptors.AccessorInlinable, location, stub.Name));
    }

    private static string? WhyNotResolved(IMethodSymbol stub, AttributeData attribute, Hosts hosts)
    {
        if (!stub.IsStatic)
            return "the stub has to be static";

        var kind = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int value ? value : -1;
        var declaredType = attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        var name = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Name").Value.Value as string ?? stub.Name;
        var isStatic = kind is StaticMethod or StaticField;

        INamedTypeSymbol owner;
        if (isStatic)
        {
            if (declaredType is null)
                return "a static member's stub names the type on the attribute: [BUTRUnsafeAccessor(kind, typeof(TheType))]";
            owner = declaredType;
        }
        else
        {
            if (stub.Parameters.Length == 0 || stub.Parameters[0].RefKind != RefKind.None || stub.Parameters[0].Type is not INamedTypeSymbol { IsReferenceType: true } instanceType)
                return "an instance member's stub takes the instance, of a class, as its first parameter";
            owner = instanceType;
        }
        var complete = hosts.Complete(owner);

        switch (kind)
        {
            case Method:
            case StaticMethod:
            {
                var arguments = (isStatic ? stub.Parameters : stub.Parameters.Skip(1)).Select(p => p.Type).ToList();
                var shape = string.Join(", ", arguments.Select(t => t.ToDisplayString()));
                var method = Hosts.SelfAndBases(complete)
                    .SelectMany(t => t.GetMembers(name).OfType<IMethodSymbol>())
                    .FirstOrDefault(m => m.MethodKind == MethodKind.Ordinary && m.Parameters.Length == arguments.Count
                        && m.Parameters.Select(p => p.Type).Zip(arguments, Hosts.SameType).All(x => x));
                if (method is null || method.IsStatic != isStatic)
                    return $"'{owner.Name}' has no {(isStatic ? "static" : "instance")} method {name}({shape})";
                if (!Hosts.SameType(method.ReturnType, stub.ReturnType) || stub.RefKind != RefKind.None)
                    return $"'{owner.Name}.{name}' returns {method.ReturnType.ToDisplayString()}, the stub {stub.ReturnType.ToDisplayString()}";
                return null;
            }

            case Field:
            case StaticField:
            {
                if (stub.Parameters.Length != (isStatic ? 0 : 1))
                    return isStatic ? "a static field's stub takes no parameters" : "a field's stub takes the instance and nothing else";
                var field = Hosts.SelfAndBases(complete).SelectMany(t => t.GetMembers(name).OfType<IFieldSymbol>()).FirstOrDefault();
                if (field is null || field.IsStatic != isStatic)
                    return $"'{owner.Name}' has no {(isStatic ? "static" : "instance")} field {name}";
                if (stub.RefKind != RefKind.Ref || !Hosts.SameType(stub.ReturnType, field.Type))
                    return $"the stub has to return ref {field.Type.ToDisplayString()}";
                return null;
            }

            default:
                return "unknown kind";
        }
    }
}
