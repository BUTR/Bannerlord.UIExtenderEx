using System.Xml;

namespace UIExtenderLib.Interface
{
    /// <summary>
    /// General interface for XML prefab patch
    /// </summary>
    public interface IPrefabPatch
    {
        string Id { get; }

        XmlDocument GetPrefabExtension();
    }
}