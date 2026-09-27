using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NUnit.Framework;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Tests;

/// <summary>
/// The attributes of UIExtenderEx 3.0 that the analyzer has rules for, as that version declares them, for the tests of
/// those rules to compile against a UIExtenderEx that does not have them yet. Built only when the UIExtenderEx referenced
/// lacks them, and in an assembly of its own, as a mod sees them. <c>[PrefabLink]</c> is not here: the analyzer package
/// supplies that one itself (<see cref="PrefabLinkAttributeGenerator"/>).
/// </summary>
internal static class NewerApi
{
    private const string Source = """
        using System;

        namespace Bannerlord.UIExtenderEx.Attributes
        {
            public enum BUTRAccessorKind
            {
                Method,
                Field,
                StaticMethod,
                StaticField,
            }

            [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            public sealed class BUTRUnsafeAccessorAttribute : Attribute
            {
                public BUTRAccessorKind Kind { get; }
                public Type? Type { get; }
                public string? Name { get; set; }

                public BUTRUnsafeAccessorAttribute(BUTRAccessorKind kind) => Kind = kind;

                public BUTRUnsafeAccessorAttribute(BUTRAccessorKind kind, Type type)
                {
                    Kind = kind;
                    Type = type;
                }
            }

            [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
            public sealed class BUTRViewModelOverrideAttribute : Attribute
            {
                public string MethodName { get; }

                public BUTRViewModelOverrideAttribute(string methodName) => MethodName = methodName;
            }
        }
        """;

    /// <summary>Null when <paramref name="references"/> have the attributes already.</summary>
    public static MetadataReference? For(ImmutableArray<MetadataReference> references)
    {
        var probe = CSharpCompilation.Create("Probe", [], references);
        if (probe.GetTypeByMetadataName("Bannerlord.UIExtenderEx.Attributes.BUTRViewModelOverrideAttribute") is not null)
            return null;

        var compilation = CSharpCompilation.Create("Bannerlord.UIExtenderEx.NewerApi",
            [CSharpSyntaxTree.ParseText(Source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.That(result.Success, Is.True, "The newer API does not compile:" + Environment.NewLine + string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
