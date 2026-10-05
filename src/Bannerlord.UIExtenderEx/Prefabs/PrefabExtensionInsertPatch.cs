using Bannerlord.UIExtenderEx.Utils;

using System;
using System.IO;
using System.Reflection;
using System.Xml;

using TaleWorlds.Engine;

using Path = System.IO.Path;

namespace Bannerlord.UIExtenderEx.Prefabs;

/// <summary>
/// Legacy patch that inserts an extension snippet as a child of the node selected via XPath.
/// </summary>
[Obsolete("Use Prefabs2.PrefabExtensionInsertPatch instead.")]
public abstract class PrefabExtensionInsertPatch : InsertPatch { }

/// <summary>
/// Legacy patch that loads an XML snippet from the module's GUI/PrefabExtensions folder and inserts it as a child.
/// </summary>
[Obsolete("Use Prefabs2.PrefabExtensionInsertPatch instead.")]
public abstract class ModulePrefabExtensionInsertPatch : PrefabExtensionInsertPatch
{
    private string Name { get; }
    private string ModuleName { get; }

    protected ModulePrefabExtensionInsertPatch(string name, string moduleName)
    {
        Name = name;
        ModuleName = moduleName;
    }

    public override XmlDocument GetPrefabExtension()
    {
        var path = Path.Combine(Utilities.GetBasePath(), "Modules", ModuleName, "GUI", "PrefabExtensions", Name + ".xml");
        var doc = new XmlDocument();

        if (File.Exists(path))
        {
            using var reader = XmlReader.Create(path, new()
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
            });
            doc.Load(reader);
        }
        else
        {
            MessageUtils.Fail($"Failed to get file {path} XML!");
        }

        if (!doc.HasChildNodes)
            MessageUtils.Fail($"Failed to parse extension ({Name}) XML!");

        return doc;
    }
}

/// <summary>
/// Legacy patch that loads an embedded resource XML snippet and inserts it as a child.
/// </summary>
[Obsolete("Use Prefabs2.PrefabExtensionInsertPatch instead.")]
public abstract class EmbedPrefabExtensionInsertPatch : PrefabExtensionInsertPatch
{
    private Assembly Assembly { get; }
    private string Path { get; }

    protected EmbedPrefabExtensionInsertPatch(Assembly assembly, string path)
    {
        Assembly = assembly;
        Path = path;
    }

    public override XmlDocument GetPrefabExtension()
    {
        using var stream = Assembly.GetManifestResourceStream(Path);
        var doc = new XmlDocument();

        if (stream is not null)
        {
            using var reader = XmlReader.Create(stream, new()
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
            });
            doc.Load(reader);
        }
        else
        {
            MessageUtils.Fail($"Failed get stream from assembly resource ({Assembly.FullName} {Path})!");
        }

        if (!doc.HasChildNodes)
            MessageUtils.Fail($"Failed to parse extension ({Assembly.FullName} {Path}) XML!");

        return doc;
    }
}