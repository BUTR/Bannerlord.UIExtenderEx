using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using System;
using System.Linq;

namespace Bannerlord.UIExtenderEx.Analyzers.Tasks;

/// <summary>
/// MSBuild task that executes <see cref="GameVersionInference"/> across resolved <c>@(ReferencePath)</c> items
/// and populates <see cref="Version"/> without failing the build if inference is unsuccessful.
/// </summary>
public sealed class InferGameVersion : Task
{
    [Required]
    public ITaskItem[] References { get; set; } = [];

    /// <summary>Gets the inferred game version string (e.g. <c>v1.4.8</c>), or an empty string if inference failed.</summary>
    [Output]
    public string Version { get; private set; } = "";

    public override bool Execute()
    {
        try
        {
            Version = GameVersionInference.Infer(References.Select(x =>
                new GameReference(x.ItemSpec, Empty(x.GetMetadata("NuGetPackageId")), Empty(x.GetMetadata("NuGetPackageVersion"))))) ?? "";
        }
        catch (Exception e)
        {
            Log.LogMessage(MessageImportance.Low, "UIExtenderEx: the game version could not be inferred: {0}", e.Message);
        }
        if (Version.Length > 0)
            Log.LogMessage(MessageImportance.Low, "UIExtenderEx: the game version the project builds against is inferred as {0}", Version);
        return true;
    }

    private static string? Empty(string value) => value.Length == 0 ? null : value;
}