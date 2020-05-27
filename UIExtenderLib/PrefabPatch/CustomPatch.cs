using System.Xml;

namespace UIExtenderLib.Interface
{
    /// <summary>
    /// Custom patch on either whole XmlDocument (if T is XmlDocument) or Xpath specified node (if XmlNode is the generic argument)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class CustomPatch<T> : IPrefabPatch where T : XmlNode
    {
        public string Id { get; }

        /// <summary>
        /// Apply this patch to obj
        /// </summary>
        /// <param name="obj"></param>
        public abstract void Apply(T obj);

        public abstract XmlDocument GetPrefabExtension();
    }
}