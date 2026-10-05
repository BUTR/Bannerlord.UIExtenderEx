using System;
using System.IO;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Locates a Mount &amp; Blade II: Bannerlord game installation directory using environment variables (<c>BANNERLORD_GAME_DIR</c>, <c>BANNERLORD_STABLE_DIR</c>) or default installation paths.
/// </summary>
public static class TestGame
{
    /// <summary>Gets the detected root directory of the game installation, or <see langword="null"/> if not found.</summary>
    public static string? Directory { get; } = new[]
        {
            Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"),
            Environment.GetEnvironmentVariable("BANNERLORD_STABLE_DIR"),
            @"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord",
        }
        .FirstOrDefault(x => !string.IsNullOrEmpty(x) && File.Exists(Path.Combine(x!, "bin", "Win64_Shipping_Client", "TaleWorlds.GauntletUI.CodeGenerator.dll")));

    /// <summary>Gets the absolute path to the game's shipping client binary directory, or <see langword="null"/> if not found.</summary>
    public static string? Bin => Directory is null ? null : Path.Combine(Directory, "bin", "Win64_Shipping_Client");
}