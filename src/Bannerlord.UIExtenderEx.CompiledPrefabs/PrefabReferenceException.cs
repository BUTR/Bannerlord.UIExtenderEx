using System;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Occurs when an assembly file on disk required for compilation references cannot be read, verified, or accessed.
/// <para>
/// Represents an assembly access error (such as file locking, deletion, or external modification by mod managers),
/// localized to movies referencing the affected assembly rather than disabling the entire compiled prefabs runtime.
/// </para>
/// </summary>
public sealed class PrefabReferenceException : Exception
{
    /// <summary>Gets the name of the assembly that could not be read.</summary>
    public string AssemblyName { get; }

    /// <summary>Initializes a new instance of the <see cref="PrefabReferenceException"/> class.</summary>
    public PrefabReferenceException(string assemblyName, string? location, Exception innerException)
        : base($"Cannot read '{assemblyName}' from '{location}': {innerException.Message}", innerException)
    {
        AssemblyName = assemblyName;
    }
}