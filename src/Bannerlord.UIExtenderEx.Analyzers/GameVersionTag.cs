using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Globalization;

namespace Bannerlord.UIExtenderEx.Analyzers;

/// <summary>
/// Prefixes diagnostic messages with the target game version tag (e.g. <c>[v1.3.4]</c>) when building multi-version projects.
/// </summary>
/// <remarks>
/// When modules compile against multiple Bannerlord versions via <c>Bannerlord.BUTRModule.Sdk</c>, version tags distinguish
/// version-specific diagnostics from findings that apply universally across builds.
/// <para>
/// Reads the version from MSBuild compiler-visible properties in priority order: <c>UIExtenderExGameVersion</c>,
/// <c>GameVersion</c>, and <c>UIExtenderExInferredGameVersion</c>. If no version property is defined, messages remain unmodified.
/// </para>
/// </remarks>
internal static class GameVersionTag
{
    public static string? Of(AnalyzerOptions options)
    {
        var global = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        foreach (var name in new[] { "build_property.UIExtenderExGameVersion", "build_property.GameVersion", "build_property.UIExtenderExInferredGameVersion" })
        {
            if (global.TryGetValue(name, out var value) && Game.GameVersions.Normalize(value) is { } version)
                return version;
        }
        return null;
    }

    /// <summary>
    /// Wraps a diagnostic reporting delegate to prepend the active game version tag to reported messages.
    /// Preserves diagnostic ID, severity, source locations, and property dictionaries to maintain code fix and suppression compatibility.
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