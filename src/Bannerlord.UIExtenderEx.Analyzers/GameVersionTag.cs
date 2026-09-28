using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Globalization;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// The game version the compilation is built against, put in front of every message as <c>[v1.3.4]</c>. A module that
/// supports several versions with <c>Bannerlord.BUTRModule.Sdk</c> is compiled once per version, each time against that
/// version's game; without it, the same warning from two builds reads alike, and a finding that holds for one version
/// only cannot be told from one that holds for all.
/// </summary>
/// <remarks>
/// Read from <c>UIExtenderExGameVersion</c>, else <c>GameVersion</c>, which the SDK and <c>Bannerlord.BuildResources</c>
/// set; the analyzer targets make both compiler-visible. With neither set, messages are left as they are.
/// </remarks>
internal static class GameVersionTag
{
    public static string? Of(AnalyzerOptions options)
    {
        var global = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        foreach (var name in new[] { "build_property.UIExtenderExGameVersion", "build_property.GameVersion" })
        {
            if (global.TryGetValue(name, out var value) && value.Trim() is { Length: > 0 } version)
                return version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? "v" + version.Substring(1) : "v" + version;
        }
        return null;
    }

    /// <summary>
    /// Reports through <paramref name="report"/> with the version in front of each message. The diagnostic is created
    /// anew from its finished message; its id, severity, locations and properties stay, so suppressions, severities set
    /// in .editorconfig and the code fixes are unaffected.
    /// </summary>
    public static Action<Diagnostic> Reporter(AnalyzerOptions options, Action<Diagnostic> report)
    {
        var version = Of(options);
        return version is null ? report : diagnostic => report(Tagged(diagnostic, version));
    }

    public static void Report(this SymbolAnalysisContext context, Diagnostic diagnostic) =>
        context.ReportDiagnostic(Of(context.Options) is { } version ? Tagged(diagnostic, version) : diagnostic);

    public static void Report(this CompilationAnalysisContext context, Diagnostic diagnostic) =>
        context.ReportDiagnostic(Of(context.Options) is { } version ? Tagged(diagnostic, version) : diagnostic);

    private static Diagnostic Tagged(Diagnostic diagnostic, string version)
    {
        var descriptor = diagnostic.Descriptor;
        return Diagnostic.Create(
            descriptor.Id,
            descriptor.Category,
            $"[{version}] {diagnostic.GetMessage(CultureInfo.InvariantCulture)}",
            diagnostic.Severity,
            diagnostic.DefaultSeverity,
            descriptor.IsEnabledByDefault,
            diagnostic.WarningLevel,
            descriptor.Title,
            descriptor.Description,
            descriptor.HelpLinkUri,
            diagnostic.Location,
            diagnostic.AdditionalLocations,
            descriptor.CustomTags,
            diagnostic.Properties);
    }
}
