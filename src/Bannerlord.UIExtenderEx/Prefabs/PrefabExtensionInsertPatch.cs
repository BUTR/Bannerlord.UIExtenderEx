using System;
using System.IO;
using System.Reflection;
using System.Xml;

using TaleWorlds.Engine;

using Path = System.IO.Path;

namespace Bannerlord.UIExtenderEx.Prefabs
{
    /// <summary>
    /// Patch that inserts prefab extension (specified by `Name`) as a child in XPath specified node, at specific position (`Position` property)
    /// </summary>
    [Obsolete("Use Prefabs2.PrefabExtensionInsertPatch instead.")]
    public abstract class PrefabExtensionInsertPatch : InsertPatch { }

    /// <summary>
    /// Patch that inserts prefab extension (specified by `Name`) as a child in XPath specified node, at specific position (`Position` property)
    /// Extension snippet should be named as `{Name}.xml` and located at module's `GUI/PrefabExtensions` folder.
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
                using var reader = XmlReader.Create(path, new XmlReaderSettings
                {
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                });
                doc.Load(reader);
            }
            else
            {
                Utils.Fail($"Failed to get file {path} XML!");
            }

            if (!doc.HasChildNodes)
                Utils.Fail($"Failed to parse extension ({Name}) XML!");

            return doc;
        }
    }

    [Obsolete("PrefabExtensionInsertPatch is obsolete")]
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
                using var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                });
                doc.Load(reader);
            }
            else
            {
                Utils.Fail($"Failed get stream from assembly resource ({Assembly.FullName} {Path})!");
            }

            if (!doc.HasChildNodes)
                Utils.Fail($"Failed to parse extension ({Assembly.FullName} {Path}) XML!");

            return doc;
        }
    }
}