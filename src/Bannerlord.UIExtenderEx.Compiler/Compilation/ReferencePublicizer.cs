using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;

/// <summary>
/// Rewrites PE metadata type visibility to public within memory buffers used exclusively for Roslyn compilation references.
/// <para>
/// Generated prefab code declares fields and parameters typed with mod ViewModel types, which are frequently declared internal.
/// Suppressing accessibility diagnostics during binding is insufficient because the C# compiler enforces type accessibility
/// rules on member signatures. Publicizing referenced types in the compile-time image eliminates declaration visibility errors,
/// while runtime execution binds to the authentic assembly under <c>[IgnoresAccessChecksTo]</c>.
/// </para>
/// Only the <c>TypeDef</c> visibility flags are modified; member accessibility is bypassed via Roslyn compiler options.
/// </summary>
public static class ReferencePublicizer
{
    private const uint VisibilityMask = 0x7;
    private const uint Public = 0x1;
    private const uint NestedPublic = 0x2;

    /// <summary>
    /// Reads an assembly from disk and publicizes all type definitions in memory, returning unmodified bytes if the file is not a managed assembly or parsing fails.
    /// </summary>
    /// <param name="path">The file path of the assembly to publicize.</param>
    /// <returns>A byte array containing the publicized or original assembly image.</returns>
    public static byte[] Publicize(string path)
    {
        var image = File.ReadAllBytes(path);
        try
        {
            using var peReader = new PEReader(new MemoryStream(image, writable: false));
            if (!peReader.HasMetadata)
                return image;

            var reader = peReader.GetMetadataReader();
            var metadataStart = peReader.PEHeaders.MetadataStartOffset;
            var tableOffset = metadataStart + reader.GetTableMetadataOffset(TableIndex.TypeDef);
            var rowSize = reader.GetTableRowSize(TableIndex.TypeDef);
            var rowCount = reader.GetTableRowCount(TableIndex.TypeDef);

            // Row 1 represents the <Module> pseudo type; the TypeAttributes flags column is the first column (4 bytes, little-endian).
            for (var row = 2; row <= rowCount; row++)
            {
                var offset = tableOffset + (row - 1) * rowSize;
                var flags = BitConverter.ToUInt32(image, offset);
                var visibility = flags & VisibilityMask;
                var isNested = visibility >= NestedPublic;
                var publicized = (flags & ~VisibilityMask) | (isNested ? NestedPublic : Public);
                if (publicized == flags)
                    continue;

                image[offset] = (byte) publicized;
                image[offset + 1] = (byte) (publicized >> 8);
                image[offset + 2] = (byte) (publicized >> 16);
                image[offset + 3] = (byte) (publicized >> 24);
            }

            return image;
        }
        catch (Exception)
        {
            return image;
        }
    }
}