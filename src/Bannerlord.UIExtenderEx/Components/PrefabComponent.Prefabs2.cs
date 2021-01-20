using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

using Bannerlord.UIExtenderEx.Prefabs2;

using TaleWorlds.Engine;

using Path = System.IO.Path;

namespace Bannerlord.UIExtenderEx.Components
{
    /// <summary>
    /// Component that deals with Gauntlet prefab XML files
    /// </summary>
    internal partial class PrefabComponent
    {
        /// <summary>
        /// Register snippet insert patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertPatch patch) => RegisterPatch(movie, xpath, node =>
        {
            if (node.OwnerDocument is not { } ownerDocument)
            {
                Utils.Fail($"XML original document for {movie} is null!");
                return;
            }

            if (!TryGetNodes(patch, out IEnumerable<XmlNode>? nodes,  out string errorMessage))
            {
                Utils.Fail(errorMessage);
                return;
            }

            XmlNode? lastPlacedNode = null;
            XmlNodeList? oldChildNodes = null;
            var currentIndex = 0;
            foreach (XmlNode rootNode in nodes!) // We know nodes is not null at this point.
            {
                if(!TryRemoveComments(rootNode))
                {
                    continue;
                }

                XmlNode importedNode = ownerDocument.ImportNode(rootNode, true);

                // Append successive nodes after the current node.
                if(lastPlacedNode != null)
                {
                    if (node.ParentNode is null)
                    {
                        Utils.Fail($"Trying to place multiple root nodes into {movie}!");
                        return;
                    }

                    var insertedNode = node.ParentNode.InsertAfter(importedNode, lastPlacedNode);
                    lastPlacedNode = insertedNode;

                    if(patch.Type == InsertType.ReplaceKeepChildren && oldChildNodes != null && patch.Index == currentIndex)
					{
                        foreach (XmlNode childNode in oldChildNodes)
                        {
                            insertedNode.AppendChild(childNode);
                        }
                    }
                }
                else
                {
                    switch ( patch.Type )
                    {
                        case InsertType.Prepend:
                            if (node.ParentNode is null)
                            {
                                Utils.Fail($"Trying to place multiple root nodes into {movie}!");
                                return;
                            }
                            lastPlacedNode = node.ParentNode.InsertBefore(importedNode, node);
                            break;

                        case InsertType.ReplaceKeepChildren:
                            if (node.ParentNode is null)
                            {
                                Utils.Fail($"Trying to place multiple root nodes into {movie}!");
                                return;
                            }
                            oldChildNodes = node.ChildNodes;
                            lastPlacedNode = node.ParentNode.ReplaceChild(importedNode, node);
                            if(patch.Index == currentIndex)
                            {
                                foreach (XmlNode childNode in oldChildNodes)
                                {
                                    lastPlacedNode.AppendChild(childNode);
                                }
                            }
                            break;

                        case InsertType.Replace:
                            if (node.ParentNode is null)
                            {
                                Utils.Fail($"Trying to place multiple root nodes into {movie}!");
                                return;
                            }
                            lastPlacedNode = node.ParentNode.ReplaceChild(importedNode, node);
                            break;

                        case InsertType.Child:
                            if(node.ChildNodes.Count == 0) // Fixes old issue where you could not insert a node as a child if the target node had no pre-existing children.
                            {
                                lastPlacedNode = node.AppendChild(importedNode);
                            }
                            else
                            {
                                int position = Math.Max(0, Math.Min(patch.Index, node.ChildNodes.Count - 1));
                                lastPlacedNode = node.InsertAfter(importedNode, node.ChildNodes[position]);
                            }
                            
                            break;

                        case InsertType.Append:
                            if (node.ParentNode is null)
                            {
                                Utils.Fail($"Trying to place multiple root nodes into {movie}!");
                                return;
                            }
                            lastPlacedNode = node.ParentNode.InsertAfter(importedNode, node);
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                ++currentIndex;
            }
        });

        private static bool TryRemoveComments(XmlNode? node)
		{
            if(node?.SelectNodes("//comment()") is not {} commentNodes)
            {
                return false;
            }

            foreach (XmlNode xmlNode in commentNodes)
            {
                // TODO: Need to confirm this, but I think the only way this happens is if the root node itself is a comment, in which case we dismiss it entirely.
                if(xmlNode.ParentNode is null)
				{
                    return false;
				}

                xmlNode.ParentNode.RemoveChild(xmlNode);
            }

            return true;
        }

        private bool TryGetNodes(PrefabExtensionInsertPatch patch, out IEnumerable<XmlNode>? nodes, out string errorMessage)
        {
            nodes = null;

            Type patchType = patch.GetType();
            MemberInfo[] contentMembers = patchType.GetMembers().Where(m => _contentAttributeTypes.Any(t => Attribute.GetCustomAttribute(m, t) is not null)).ToArray();

            // Validate single members with Content attribute.
            if (contentMembers.Length != 1)
            {
                errorMessage = $"{patch.GetType().Name} contains {contentMembers.Length} members with Content Attributes. " +
                               $"Insertion Patches must contain a single property or method with a {nameof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute)}.";
                return false;
            }

            Attribute[] contentAttributes = _contentAttributeTypes.Select(t => Attribute.GetCustomAttribute(contentMembers[0], t)).Where(a => a != null).ToArray();

            // Validate member has single content attribute.
            if(contentAttributes.Length != 1)
            {
                errorMessage = $"{contentMembers[0].Name} in {patch.GetType().Name} contains {contentAttributes.Length} attributes of type " +
                               $"{nameof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute)}. Should only have a single Content attribute.";
                return false;
            }

            errorMessage = $"{contentMembers[0].Name} in {patch.GetType().Name} ";
            nodes = contentAttributes[0] switch
            {
                PrefabExtensionInsertPatch.PrefabExtensionXmlDocumentAttribute attribute => GetNodes(contentMembers[0], attribute, ref errorMessage),
                PrefabExtensionInsertPatch.PrefabExtensionXmlNodeAttribute attribute => GetNodes(contentMembers[0], attribute, ref errorMessage),
                PrefabExtensionInsertPatch.PrefabExtensionXmlNodesAttribute attribute => GetNodes(contentMembers[0], attribute, ref errorMessage),
                PrefabExtensionInsertPatch.PrefabExtensionTextAttribute attribute => GetNodes(contentMembers[0], attribute, ref errorMessage),
                PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute attribute => GetNodes(contentMembers[0], attribute, ref errorMessage),
                _ => throw new ArgumentOutOfRangeException(nameof(contentAttributes), contentAttributes[0], null)
            };

            if(nodes is null)
            {
                return false;
			}

            errorMessage = "";
            return true;
        }

        private static IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo, PrefabExtensionInsertPatch.PrefabExtensionXmlDocumentAttribute attribute, ref string errorMessage)
        {
            if(!TryGetContent(contentMemberInfo, ref errorMessage, out XmlDocument? xmlDocument) || xmlDocument is null)
            {
                return null;
            }

            return attribute.RemoveRootNode ? xmlDocument.ChildNodes.Cast<XmlNode>() : new List<XmlNode> {xmlDocument};
        }

        private static IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo, PrefabExtensionInsertPatch.PrefabExtensionXmlNodeAttribute attribute, ref string errorMessage)
        {
            if (!TryGetContent(contentMemberInfo, ref errorMessage, out XmlNode? xmlNode) || xmlNode is null)
            {
                return null;
            }

            return attribute.RemoveRootNode ? xmlNode.ChildNodes.Cast<XmlNode>() : new List<XmlNode> { xmlNode };
        }

        private static IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo, PrefabExtensionInsertPatch.PrefabExtensionXmlNodesAttribute attribute, ref string errorMessage)
        {
            return !TryGetContent(contentMemberInfo, ref errorMessage, out IEnumerable<XmlNode>? xmlNodes) ? null : xmlNodes;
        }

        private static IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo, PrefabExtensionInsertPatch.PrefabExtensionTextAttribute attribute, ref string errorMessage)
        {
            if (!TryGetContent(contentMemberInfo, ref errorMessage, out string? text) || text is null)
            {
                return null;
            }

            XmlDocument document = new();
            try
            {
                document.LoadXml(text);
            }
            catch ( XmlException e )
            {
                errorMessage += $"failed to load or parse. Exception: {e}";
                return null;
            }

            return attribute.RemoveRootNode ? document.ChildNodes.Cast<XmlNode>() : new List<XmlNode> { document };
        }

        private IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo, PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute attribute, ref string errorMessage)
        {
            if (!TryGetContent(contentMemberInfo, ref errorMessage, out string? fileName) || fileName is null)
            {
                return null;
            }

            fileName = Path.GetFileNameWithoutExtension(fileName);

            string moduleDirectoryPath = Path.Combine(Utilities.GetBasePath(), "Modules", _moduleName, "GUI");
            string[] files = Directory.GetFiles(moduleDirectoryPath, "*.xml", SearchOption.AllDirectories);
            files = files.Where(x => string.Equals(Path.GetFileNameWithoutExtension(x), fileName, StringComparison.InvariantCultureIgnoreCase)).ToArray();
            if (files.Length != 1)
            {
                errorMessage += $"Found {files.Length} files matching {fileName}.";
                return null;
            }

            XmlDocument document = new();
            try
            {
                document.Load(files[0]);
            }
            catch (Exception e)
            {
                errorMessage += $"exception was thrown while loading the document. Exception: {e}";
                return null;
            }

            return attribute.RemoveRootNode ? document.ChildNodes.Cast<XmlNode>() : new List<XmlNode> { document };
        }

        private static bool TryGetContent<T>(MemberInfo memberInfo, ref string errorMessage, out T? output)
        {
            output = default;
            
            var value = GetFunction(typeof(T), memberInfo)();
            if (value is null)
            {
                Type memberType = memberInfo is PropertyInfo propertyInfo ? propertyInfo.PropertyType : ((MethodInfo)memberInfo).ReturnType;
                errorMessage += $"is of type: {memberType.Name}. A Member flagged with a Content attribute must be " +
                                $"of one of the types listed in {nameof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute)}";
                return false;
            }

            if (value is not T castContent)
            {
                Type memberType = memberInfo is PropertyInfo propertyInfo ? propertyInfo.PropertyType : ((MethodInfo)memberInfo).ReturnType;
                errorMessage += $"is of type: {memberType.Name}, while its attribute type expects a {typeof(T).Name}. " +
                                $"See {nameof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute)} for more information.";
                return false;
            }

            errorMessage = "";
            output = castContent;

            return true;
        }

        /// <summary>
        /// Register snippet set attribute patch
        /// </summary>
        /// <param name="movie"></param>
        /// <param name="xpath"></param>
        /// <param name="patch"></param>
        public void RegisterPatch(string movie, string? xpath, PrefabExtensionSetAttributePatch patch) => RegisterPatch(movie, xpath, node =>
        {
            if (node.OwnerDocument is not { } ownerDocument)
            {
                return;
            }

            if (node.NodeType != XmlNodeType.Element)
            {
                return;
            }

            foreach (var attribute in patch.Attributes)
            {
                if (node.Attributes![attribute.Name] is null)
                {
                    var newAttribute = ownerDocument.CreateAttribute(attribute.Name);
                    node.Attributes.Append(newAttribute);
                }

                node.Attributes![attribute.Name].Value = attribute.Value;
            }
        });

        private readonly IReadOnlyList<Type> _contentAttributeTypes = new List<Type>
        {
            typeof(PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute),
            typeof(PrefabExtensionInsertPatch.PrefabExtensionTextAttribute),
            typeof(PrefabExtensionInsertPatch.PrefabExtensionXmlNodeAttribute),
            typeof(PrefabExtensionInsertPatch.PrefabExtensionXmlNodesAttribute),
            typeof(PrefabExtensionInsertPatch.PrefabExtensionXmlDocumentAttribute)
        };

        private delegate string StringSignature();
        private delegate XmlNode XmlNodeSignature();
        private delegate XmlDocument XmlDocumentSignature();
        private delegate IEnumerable<XmlNode> IEnumerableXmlNodeSignature();

        private static Func<object?> GetFunction(Type returnType, MemberInfo memberInfo)
        {
            return memberInfo switch
            {
                PropertyInfo propertyInfo => GetFunction(returnType, propertyInfo),
                MethodInfo methodInfo => GetFunction(returnType, methodInfo),
                _ => () => null
            };
        }

        private static Func<object?> GetFunction(Type returnType, PropertyInfo propertyInfo) =>
            GetFunction(returnType, propertyInfo.GetMethod);

        private static Func<object?> GetFunction(Type returnType, MethodInfo methodInfo)
        {
            if (returnType == typeof(string))
            {
                var @delegate = AccessTools3.GetDelegate<StringSignature>(methodInfo);
                return () => @delegate?.Invoke();
            }
            if (returnType == typeof(XmlNode))
            {
                var @delegate = AccessTools3.GetDelegate<XmlNodeSignature>(methodInfo);
                return () => @delegate?.Invoke();
            }
            if (returnType == typeof(XmlDocument))
            {
                var @delegate = AccessTools3.GetDelegate<XmlDocumentSignature>(methodInfo);
                return () => @delegate?.Invoke();
            }
            if (returnType == typeof(IEnumerable<XmlNode>))
            {
                var @delegate = AccessTools3.GetDelegate<IEnumerableXmlNodeSignature>(methodInfo);
                return () => @delegate?.Invoke();
            }

            return () => null;
        }
    }
}