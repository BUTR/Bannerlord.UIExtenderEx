using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>Represents an assembly compilation reference inspected from disk metadata, including unresolved dependencies not yet loaded into the current runtime.</summary>
public sealed record PrefabAssemblyReference(
    string Name, string Path, string Mvid, Version Version, IReadOnlyList<PrefabAssemblyReference.Dependency> Dependencies, bool DefinesVector2)
{
    /// <summary>
    /// Represents a declared assembly reference dependency, specifying its simple assembly name and requested version.
    /// </summary>
    public sealed record Dependency(string Name, Version Version);

    private sealed record Entry(long Length, DateTime Modified, PrefabAssemblyReference Reference);
    private static readonly ConcurrentDictionary<string, Entry> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads assembly metadata from the on-disk location of the specified loaded assembly and verifies that its MVID matches.</summary>
    public static PrefabAssemblyReference Read(Assembly assembly)
    {
        var reference = Read(assembly.Location);
        if (reference.Mvid != assembly.ManifestModule.ModuleVersionId.ToString("N"))
            throw new InvalidOperationException($"Assembly '{assembly.FullName}' changed on disk after it was loaded.");
        return reference;
    }

    /// <summary>Attempts to read assembly metadata from the specified file path, returning <see langword="null"/> if reading fails due to I/O or format errors.</summary>
    public static PrefabAssemblyReference? TryRead(string path)
    {
        try { return Read(path); }
        catch (Exception e) when (e is IOException or BadImageFormatException or TypeLoadException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Reads assembly metadata from the specified file path, utilizing cached metadata when file length and modification timestamps match.</summary>
    public static PrefabAssemblyReference Read(string path)
    {
        var file = new FileInfo(path);
        if (Cache.TryGetValue(file.FullName, out var cached) && cached.Length == file.Length && cached.Modified == file.LastWriteTimeUtc)
            return cached.Reference;
        var reference = ReadMetadata(file.FullName);
        Cache[file.FullName] = new(file.Length, file.LastWriteTimeUtc, reference);
        return reference;
    }

    private static PrefabAssemblyReference ReadMetadata(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var definition = reader.GetAssemblyDefinition();
        var dependencies = reader.AssemblyReferences.Select(handle =>
        {
            var reference = reader.GetAssemblyReference(handle);
            return new Dependency(reader.GetString(reference.Name), reference.Version);
        }).ToList();
        var definesVector2 = reader.TypeDefinitions.Any(handle =>
        {
            var type = reader.GetTypeDefinition(handle);
            return reader.GetString(type.Name) == "Vector2" && reader.GetString(type.Namespace) == "System.Numerics";
        });
        return new(reader.GetString(definition.Name), path,
            reader.GetGuid(reader.GetModuleDefinition().Mvid).ToString("N"), definition.Version, dependencies, definesVector2);
    }
}