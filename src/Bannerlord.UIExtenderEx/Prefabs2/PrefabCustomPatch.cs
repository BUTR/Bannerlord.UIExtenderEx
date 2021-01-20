using System.Xml;

namespace Bannerlord.UIExtenderEx.Prefabs2
{
    /// <summary>
    /// Custom patch on either whole XmlDocument (if T is XmlDocument) or Xpath specified node (if XmlNode is the generic argument)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class PrefabCustomPatch<T> where T : XmlNode
    {
        /// <summary>
        /// Apply this patch to obj
        /// </summary>
        /// <param name="obj"></param>
        public abstract void Apply(T obj);
    }
}