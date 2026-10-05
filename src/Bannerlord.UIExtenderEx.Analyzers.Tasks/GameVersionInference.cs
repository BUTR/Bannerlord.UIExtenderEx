using FetchBannerlordVersion;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Tasks;

/// <summary>Represents an assembly reference evaluated during build, including file path and originating NuGet package identity.</summary>
public readonly struct GameReference
{
    public GameReference(string path, string? packageId, string? packageVersion)
    {
        Path = path;
        PackageId = packageId;
        PackageVersion = packageVersion;
    }

    public string Path { get; }

    public string? PackageId { get; }

    public string? PackageVersion { get; }
}

/// <summary>
/// Infers the targeted game version from resolved compilation references when no explicit property is set.
/// Evaluates <c>Bannerlord.ReferenceAssemblies</c> package IDs and versions, or reads local game installations
/// containing <c>TaleWorlds.Library.dll</c> using <c>FetchBannerlordVersion</c>.
/// </summary>
public static class GameVersionInference
{
    public const string LibraryFileName = "TaleWorlds.Library.dll";

    private const string PackagePrefix = "Bannerlord.ReferenceAssemblies";

    /// <summary>Infers the normalized game version string, prioritizing <c>TaleWorlds.Library.dll</c> references.</summary>
    public static string? Infer(IEnumerable<GameReference> references)
    {
        var ordered = references.OrderBy(x => IsLibrary(x.Path) ? 0 : 1).ToList();
        foreach (var reference in ordered)
        {
            if (FromPackage(reference.PackageId, reference.PackageVersion) is { } version)
                return version;
        }
        foreach (var reference in ordered.Where(x => IsLibrary(x.Path) && string.IsNullOrEmpty(x.PackageId)))
        {
            if (FromLibrary(reference.Path) is { } version)
                return version;
        }
        return null;
    }

    /// <summary>
    /// Extracts and normalizes the game version from a <c>Bannerlord.ReferenceAssemblies</c> package ID and version.
    /// </summary>
    public static string? FromPackage(string? packageId, string? packageVersion)
    {
        if (packageId is null || packageVersion is null || !packageId.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var prefix = packageId.EndsWith(".EarlyAccess", StringComparison.OrdinalIgnoreCase) ? "e" : "v";
        return Normalize(prefix + packageVersion.Split('-')[0]);
    }

    /// <summary>
    /// Infers the game version from a local file path to <c>TaleWorlds.Library.dll</c> using <c>FetchBannerlordVersion</c>.
    /// </summary>
    public static string? FromLibrary(string libraryPath)
    {
        try
        {
            if (Path.GetDirectoryName(Path.GetFullPath(libraryPath)) is not { } binaries
                || Path.GetDirectoryName(binaries) is not { } bin
                || Path.GetDirectoryName(bin) is not { } game)
                return null;
            return Normalize(Fetcher.GetVersion(game, LibraryFileName));
        }
        catch (Exception)
        {
            // A layout or an assembly it does not know: nothing to infer from
            return null;
        }
    }

    /// <summary>
    /// Normalizes raw version strings (e.g. <c>v1.2.12.66233</c>, <c>1.4.8</c>, <c>e1.8.1</c>) to standard three-part versions (e.g. <c>v1.2.12</c>, <c>v1.4.8</c>, <c>e1.8.1</c>).
    /// </summary>
    public static string? Normalize(string? version)
    {
        var trimmed = version?.Trim() ?? "";
        if (trimmed.Length == 0)
            return null;
        var prefix = char.IsLetter(trimmed[0]) ? char.ToLowerInvariant(trimmed[0]) : 'v';
        var parts = (char.IsLetter(trimmed[0]) ? trimmed.Substring(1) : trimmed).Split('.');
        if (parts.Length < 3 || parts.Take(3).Any(x => x.Length == 0 || !x.All(char.IsDigit)))
            return null;
        return prefix + string.Join(".", parts.Take(3));
    }

    private static bool IsLibrary(string path) => string.Equals(Path.GetFileName(path), LibraryFileName, StringComparison.OrdinalIgnoreCase);
}