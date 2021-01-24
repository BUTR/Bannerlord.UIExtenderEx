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
        private readonly Lazy<IReadOnlyList<Type>> _contentAttributeTypes = new(() =>
        {
            Type contentAttributeType = typeof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute);
            return contentAttributeType.Assembly.GetTypes().Where(t => !t.IsAbstract && contentAttributeType.IsAssignableFrom(t)).ToList();
        });

        public delegate string StringSignature();
        public delegate XmlNode XmlNodeSignature();
        public delegate XmlDocument XmlDocumentSignature();
        public delegate IEnumerable<XmlNode> IEnumerableXmlNodeSignature();

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

            if (!TryGetNodes(patch, out IEnumerable<XmlNode>? nodes, out string errorMessage))
            {
                Utils.Fail(errorMessage);
                return;
            }

            if (patch.Type != InsertType.Child && node.ParentNode is null)
            {
                Utils.Fail($"Trying to place multiple root nodes into {movie}!");
                return;
            }

            XmlNode? lastPlacedNode = null;
            XmlNodeList? oldChildNodes = null;
            XmlNode[] nodesArray = nodes!.ToArray();
            for (var i = 0; i < nodesArray.Length; ++i)
            {
                XmlNode currentNode = nodesArray[i];
                if (!TryRemoveComments(currentNode))
                {
                    continue;
                }

                XmlNode importedNode = ownerDocument!.ImportNode(currentNode, true);

                if (i == 0)
                {
                    // Insert initial node.
                    lastPlacedNode = patch.Type switch
                    {
                        InsertType.Prepend => node!.ParentNode!.InsertBefore(importedNode, node),
                        InsertType.ReplaceKeepChildren => ReplaceKeepChildren(node, importedNode, patch.Index == 0 || nodesArray.Length == 1, out oldChildNodes),
                        InsertType.Replace => ReplaceNode(node, importedNode),
                        InsertType.Child => InsertAsChild(node, importedNode, patch.Index),
                        InsertType.Append => node!.ParentNode!.InsertAfter(importedNode, node),
                        _ => throw new ArgumentOutOfRangeException()
                    };
                }
                else
                {

                    // Append successive nodes after the current node.
                    var insertedNode = lastPlacedNode!.ParentNode!.InsertAfter(importedNode, lastPlacedNode);
                    if (patch.Type == InsertType.ReplaceKeepChildren && oldChildNodes != null && patch.Index == i)
                    {
                        foreach (XmlNode childNode in oldChildNodes)
                        {
                            insertedNode.AppendChild(childNode);
                        }
                    }
                    lastPlacedNode = insertedNode;
                }
            }
        });

        private static XmlNode ReplaceNode(XmlNode targetNode, XmlNode importedNode)
        {
            targetNode!.ParentNode!.ReplaceChild(importedNode, targetNode);
            return importedNode;
        }

        private static XmlNode ReplaceKeepChildren(XmlNode targetNode, XmlNode importedNode, bool appendChildren, out XmlNodeList oldChildNodes)
        {
            oldChildNodes = targetNode.ChildNodes;
            targetNode.ParentNode!.ReplaceChild(importedNode, targetNode);
            if (appendChildren)
            {
                while (oldChildNodes.Count > 0)
                {
                    importedNode.AppendChild(oldChildNodes.Item(0)!);
                }
            }

            return importedNode;
        }
        private static XmlNode InsertAsChild(XmlNode targetNode, XmlNode importedNode, int index)
        {
            if (targetNode.ChildNodes.Count == 0)
            {
                // Fixes issue in original API where you could not insert a node as a child if the target node had no pre-existing children.
                return targetNode.AppendChild(importedNode);
            }

            if (index >= targetNode.ChildNodes.Count)
            {
                // Fixes issue in original API where you could not insert a node as the last child of the target node.
                return targetNode.InsertAfter(importedNode, targetNode.ChildNodes[targetNode.ChildNodes.Count - 1]);
            }

            return targetNode.InsertBefore(importedNode, targetNode.ChildNodes[Math.Max(0, index)]);
        }

        /// <summary>
        /// Performs validation on <paramref name="patch"/> class, and returns true if everything is okay.
        /// </summary>
        private bool TryGetNodes(PrefabExtensionInsertPatch patch, out IEnumerable<XmlNode>? nodes, out string errorMessage)
        {
            nodes = null;

            Type patchType = patch.GetType();
            MemberInfo[] contentMembers = patchType.GetMembers().Where(m => _contentAttributeTypes.Value.Any(t => Attribute.GetCustomAttribute(m, t) is not null)).ToArray();

            // Validate single members with Content attribute.
            if (contentMembers.Length != 1)
            {
                errorMessage = $"{patch.GetType().Name} contains {contentMembers.Length} members with Content Attributes. " +
                               $"Insertion Patches must contain a single property or method with a {nameof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute)}.";
                return false;
            }

            Attribute[] contentAttributes = _contentAttributeTypes.Value.Select(t => Attribute.GetCustomAttribute(contentMembers[0], t)).Where(a => a != null).ToArray();

            // Validate member has single content attribute.
            if (contentAttributes.Length != 1)
            {
                errorMessage = $"{contentMembers[0].Name} in {patch.GetType().Name} contains {contentAttributes.Length} attributes of type " +
                               $"{nameof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute)}. Should only have a single Content attribute.";
                return false;
            }

            errorMessage = $"{contentMembers[0].Name} in {patch.GetType().Name} ";
            nodes = contentAttributes[0] switch
            {
                PrefabExtensionInsertPatch.PrefabExtensionXmlDocumentAttribute attribute => GetNodes(contentMembers[0], attribute, patch, ref errorMessage),
                PrefabExtensionInsertPatch.PrefabExtensionXmlNodeAttribute attribute => GetNodes(contentMembers[0], attribute, patch, ref errorMessage),
                PrefabExtensionInsertPatch.PrefabExtensionXmlNodesAttribute attribute => GetNodes(contentMembers[0], attribute, patch, ref errorMessage),
                PrefabExtensionInsertPatch.PrefabExtensionTextAttribute attribute => GetNodes(contentMembers[0], attribute, patch, ref errorMessage),
                PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute attribute => GetNodes(contentMembers[0], attribute, patch, ref errorMessage),
                _ => throw new ArgumentOutOfRangeException(nameof(contentAttributes), contentAttributes[0], null)
            };

            if (nodes is null)
            {
                return false;
            }

            errorMessage = "";
            return true;
        }

        /// <summary>
        /// Validates that a method or property flagged with <see cref="PrefabExtensionInsertPatch.PrefabExtensionXmlDocumentAttribute"/>
        /// is of type <see cref="XmlDocument"/>, then retrieves its nodes if everything is okay.
        /// </summary>
        private static IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo,
            PrefabExtensionInsertPatch.PrefabExtensionXmlDocumentAttribute attribute,
            PrefabExtensionInsertPatch instance,
            ref string errorMessage)
        {
            if (!TryGetContent(contentMemberInfo, instance, ref errorMessage, out XmlDocument? xmlDocument) || xmlDocument is null)
            {
                return null;
            }

            return attribute.RemoveRootNode ? xmlDocument.DocumentElement!.ChildNodes.Cast<XmlNode>() : new List<XmlNode> { xmlDocument.DocumentElement };
        }

        /// <summary>
        /// Validates that a method or property flagged with <see cref="PrefabExtensionInsertPatch.PrefabExtensionXmlNodeAttribute"/>
        /// is of type <see cref="XmlNode"/>, then retrieves its nodes if everything is okay.
        /// </summary>
        private static IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo,
            PrefabExtensionInsertPatch.PrefabExtensionXmlNodeAttribute attribute,
            PrefabExtensionInsertPatch instance,
            ref string errorMessage)
        {
            if (!TryGetContent(contentMemberInfo, instance, ref errorMessage, out XmlNode? xmlNode) || xmlNode is null)
            {
                return null;
            }

            // Catches potential issue where XmlDocuments cannot be imported into other documents.
            if (xmlNode is XmlDocument document)
            {
                xmlNode = document.DocumentElement;
            }

            return attribute.RemoveRootNode ? xmlNode!.ChildNodes.Cast<XmlNode>() : new List<XmlNode> { xmlNode };
        }

        /// <summary>
        /// Validates that a method or property flagged with <see cref="PrefabExtensionInsertPatch.PrefabExtensionXmlNodesAttribute"/>
        /// is an <see cref="IEnumerable{T}"/> of type <see cref="XmlNode"/>, then retrieves its nodes if everything is okay.
        /// </summary>
        // ReSharper disable once UnusedParameter.Local
        private static IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo,
            PrefabExtensionInsertPatch.PrefabExtensionXmlNodesAttribute attribute,
            PrefabExtensionInsertPatch instance,
            ref string errorMessage)
        {
            XmlNode[]? result = !TryGetContent(contentMemberInfo, instance, ref errorMessage, out IEnumerable<XmlNode>? xmlNodes) ? null : xmlNodes!.ToArray();

            // Catches potential issue where XmlDocuments cannot be imported into other documents.
            if (result is not null)
            {
                for (var i = 0; i < result.Length; i++)
                {
                    if (result[i] is XmlDocument document)
                    {
                        result[i] = document.DocumentElement!;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Validates that a method or property flagged with <see cref="PrefabExtensionInsertPatch.PrefabExtensionTextAttribute"/>
        /// is of type <see cref="string"/>, then retrieves its nodes if everything is okay.
        /// </summary>
        private static IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo,
            PrefabExtensionInsertPatch.PrefabExtensionTextAttribute attribute,
            PrefabExtensionInsertPatch instance,
            ref string errorMessage)
        {
            if (!TryGetContent(contentMemberInfo, instance, ref errorMessage, out string? text) || text is null)
            {
                return null;
            }

            XmlDocument document = new();
            try
            {
                document.LoadXml(text);
            }
            catch (XmlException e)
            {
                errorMessage += $"failed to load or parse. Exception: {e}";
                return null;
            }

            return attribute.RemoveRootNode ? document.DocumentElement!.ChildNodes.Cast<XmlNode>() : new List<XmlNode> { document.DocumentElement };
        }

        /// <summary>
        /// Validates that a method or property flagged with <see cref="PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute"/>
        /// is of type <see cref="string"/>, then attempts to load its nodes from file.
        /// </summary>
        private IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo,
            PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute attribute,
            PrefabExtensionInsertPatch instance,
            ref string errorMessage)
        {
            XmlDocument document = new();
            try
            {
                if (!TryGetContent(contentMemberInfo, instance, ref errorMessage, out string? fileName) || fileName is null)
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

                document.Load(files[0]);
            }
            catch (Exception e)
            {
                errorMessage += $"exception was thrown while loading the document. Exception: {e}";
                return null;
            }

            return attribute.RemoveRootNode ? document.DocumentElement!.ChildNodes.Cast<XmlNode>() : new List<XmlNode> { document.DocumentElement };
        }

        /// <summary>
        /// Validates that the Property/Method specified in <paramref name="memberInfo"/> is of type <typeparamref name="T"/>.
        /// Returns true if everything is okay, and outputs the cast content in <paramref name="output"/>.
        /// </summary>
        private static bool TryGetContent<T>(MemberInfo memberInfo,
            PrefabExtensionInsertPatch instance,
            ref string errorMessage,
            out T? output)
        {
            output = default;

            var value = GetFunction(typeof(T), instance, memberInfo)();
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
                    var newAttribute = ownerDocument!.CreateAttribute(attribute.Name);
                    node.Attributes.Append(newAttribute);
                }

                node.Attributes![attribute.Name].Value = attribute.Value;
            }
        });

        private static Func<object?> GetFunction(Type returnType,
            PrefabExtensionInsertPatch instance,
            MemberInfo memberInfo)
        {
            var methodInfo = memberInfo switch
            {
                PropertyInfo pi => pi.GetMethod,
                MethodInfo mi => mi,
                _ => null
            };

            if(methodInfo is null)
            {
                return () => null;
            }

            if (returnType == typeof(string))
            {
                var @delegate = Delegate.CreateDelegate(typeof(StringSignature), instance, methodInfo) as StringSignature;
                return () => @delegate?.Invoke();
            }
            if (returnType == typeof(XmlNode))
            {
                var @delegate = Delegate.CreateDelegate(typeof(XmlNodeSignature), instance, methodInfo) as XmlNodeSignature;
                return () => @delegate?.Invoke();
            }
            if (returnType == typeof(XmlDocument))
            {
                var @delegate = Delegate.CreateDelegate(typeof(XmlDocumentSignature), instance, methodInfo) as XmlDocumentSignature;
                return () => @delegate?.Invoke();
            }
            if (returnType == typeof(IEnumerable<XmlNode>))
            {
                var @delegate = Delegate.CreateDelegate(typeof(IEnumerableXmlNodeSignature), instance, methodInfo) as IEnumerableXmlNodeSignature;
                return () => @delegate?.Invoke();
            }

            return () => null;
        }
    }
}