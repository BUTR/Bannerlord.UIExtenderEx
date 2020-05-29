using System.Diagnostics;
using System.Reflection;
using System.Xml;

using TaleWorlds.Engine;

using Path = System.IO.Path;

namespace Bannerlord.UIExtenderEx.Prefabs
{
    /// <summary>
    /// Patch that inserts prefab extension (specified by `Name`) as a child in XPath specified node, at specific position (`Position` property)
    /// </summary>
    public abstract class PrefabExtensionInsertPatch : InsertPatch
    {

    }

    /// <summary>
    /// Patch that inserts prefab extension (specified by `Name`) as a child in XPath specified node, at specific position (`Position` property)
    /// Extension snippet should be named as `{Name}.xml` and located at module's `GUI/PrefabExtensions` folder.
    /// </summary>
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

            using var reader = XmlReader.Create(path, new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
            });
            doc.Load(reader);

            Debug.Assert(doc.HasChildNodes, $"Failed to parse extension ({Name}) XML!");
            return doc;
        }
    }

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
            doc.Load(stream);

            Debug.Assert(doc.HasChildNodes, $"Failed to parse extension ({Assembly.FullName} {Path}) XML!");
            return doc;
        }
    }
}