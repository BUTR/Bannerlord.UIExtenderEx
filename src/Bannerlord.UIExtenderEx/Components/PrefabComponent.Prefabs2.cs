using Bannerlord.BUTR.Shared.Helpers;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.Utils;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Components;

/// <summary>
/// Manages registration, filtering, and execution of Gauntlet prefab XML patches.
/// </summary>
internal partial class PrefabComponent
{
    private readonly Lazy<IReadOnlyList<Type>> _contentAttributeTypes = new(() =>
    {
        var contentAttributeType = typeof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute);
        return [.. contentAttributeType.Assembly.GetTypes().Where(t => !t.IsAbstract && contentAttributeType.IsAssignableFrom(t))];
    });

    private delegate string StringSignature();
    private delegate XmlNode XmlNodeSignature();
    private delegate XmlDocument XmlDocumentSignature();
    private delegate IEnumerable<XmlNode> IEnumerableXmlNodeSignature();

    /// <summary>
    /// Registers a v2 snippet insert patch targeting the node selected via <paramref name="xpath"/>.
    /// </summary>
    /// <param name="movie">The target movie name.</param>
    /// <param name="xpath">The XPath expression selecting the target node.</param>
    /// <param name="patch">The insert patch definition.</param>
    public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertPatch patch) => RegisterPatch(movie, xpath, patch.GetType(), node =>
    {
        // Remove patches do not require content; earlier versions required a dummy content member.
        if (patch.Type == InsertType.Remove)
        {
            if (node.ParentNode is not { } parentNode)
            {
                MessageUtils.Fail($"Trying to remove the root node of {movie}!");
                return;
            }

            parentNode.RemoveChild(node);
            return;
        }

        if (!TryGetNodes(patch, out var nodes, out var errorMessage))
        {
            MessageUtils.Fail(errorMessage);
            return;
        }

        PlaceNodes(movie, node, [.. nodes], patch.Type, patch.Index);
    });

    /// <summary>
    /// Inserts <paramref name="nodes"/> relative to <paramref name="node"/> based on the specified <see cref="InsertType"/> and <paramref name="index"/>.
    /// </summary>
    private static void PlaceNodes(string movie, XmlNode node, IReadOnlyList<XmlNode> nodes, InsertType type, int index)
    {
        if (node.OwnerDocument is not { } ownerDocument)
        {
            MessageUtils.Fail($"XML original document for {movie} is null!");
            return;
        }

        if (type != InsertType.Child && node.ParentNode is null)
        {
            MessageUtils.Fail($"Trying to place multiple root nodes into {movie}!");
            return;
        }

        var lastPlacedNode = default(XmlNode);
        var oldChildNodes = default(XmlNodeList);
        var firstNodeInserted = false;
        for (var i = 0; i < nodes.Count; ++i)
        {
            var currentNode = nodes[i];
            if (!TryRemoveComments(currentNode))
            {
                continue;
            }

            var importedNode = ownerDocument.ImportNode(currentNode, true);

            if (!firstNodeInserted)
            {
                firstNodeInserted = true;
                // Insert initial node.
                lastPlacedNode = type switch
                {
                    InsertType.Prepend => node.ParentNode?.InsertBefore(importedNode, node),
                    InsertType.ReplaceKeepChildren => ReplaceKeepChildren(node, importedNode, index == 0 || nodes.Count == 1, out oldChildNodes),
                    InsertType.Replace => ReplaceNode(node, importedNode),
                    InsertType.Child => InsertAsChild(node, importedNode, index),
                    InsertType.Append => node.ParentNode?.InsertAfter(importedNode, node),
                    InsertType.Remove => node.ParentNode?.RemoveChild(node),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            else
            {
                // Append successive nodes after the current node.
                if (lastPlacedNode?.ParentNode is not { } lastPlacedParentNode)
                {
                    MessageUtils.Fail($"Failed to place the nodes of the patch into {movie}!");
                    return;
                }

                var insertedNode = lastPlacedParentNode.InsertAfter(importedNode, lastPlacedNode);
                if (type == InsertType.ReplaceKeepChildren && oldChildNodes != null && index == i)
                {
                    foreach (XmlNode childNode in oldChildNodes)
                    {
                        insertedNode.AppendChild(childNode);
                    }
                }
                lastPlacedNode = insertedNode;
            }
        }
    }

    private static XmlNode? ReplaceNode(XmlNode targetNode, XmlNode importedNode)
    {
        if (targetNode.ParentNode is not { } parentNode)
        {
            return null;
        }

        parentNode.ReplaceChild(importedNode, targetNode);
        return importedNode;
    }

    private static XmlNode? ReplaceKeepChildren(XmlNode targetNode, XmlNode importedNode, bool appendChildren, out XmlNodeList oldChildNodes)
    {
        oldChildNodes = targetNode.ChildNodes;
        if (targetNode.ParentNode is not { } parentNode)
        {
            return null;
        }

        parentNode.ReplaceChild(importedNode, targetNode);
        if (appendChildren)
        {
            while (oldChildNodes.Count > 0 && oldChildNodes.Item(0) is { } oldChildNode)
            {
                importedNode.AppendChild(oldChildNode);
            }
        }

        return importedNode;
    }
    private static XmlNode InsertAsChild(XmlNode targetNode, XmlNode importedNode, int index)
    {
        if (targetNode.ChildNodes.Count == 0)
        {
            // Appends child if target element has no pre-existing children.
            return targetNode.AppendChild(importedNode);
        }

        if (index >= targetNode.ChildNodes.Count)
        {
            // Appends child after the last existing child if index equals or exceeds child count.
            return targetNode.InsertAfter(importedNode, targetNode.ChildNodes[targetNode.ChildNodes.Count - 1]);
        }

        return targetNode.InsertBefore(importedNode, targetNode.ChildNodes[Math.Max(0, index)]);
    }

    /// <summary>
    /// Validates the structure and content annotations of <paramref name="patch"/>, extracting its XML nodes.
    /// </summary>
    private bool TryGetNodes(PrefabExtensionInsertPatch patch, [NotNullWhen(true)] out IEnumerable<XmlNode>? nodes, out string errorMessage)
    {
        nodes = null;

        var patchType = patch.GetType();
        var contentMembers = patchType.GetMembers().Where(m => _contentAttributeTypes.Value.Any(t => Attribute.GetCustomAttribute(m, t) is not null)).ToArray();

        // Validate single members with Content attribute.
        if (contentMembers.Length != 1)
        {
            errorMessage = $"{patch.GetType().Name} contains {contentMembers.Length} members with Content Attributes. " +
                           $"Insertion Patches must contain a single property or method with a {nameof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute)}.";
            return false;
        }

        var contentAttributes = _contentAttributeTypes.Value.Select(t => Attribute.GetCustomAttribute(contentMembers[0], t)).Where(a => a != null).ToArray();

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
#pragma warning disable CS0618 // Still honoured for existing patches
            PrefabExtensionInsertPatch.PrefabExtensionXmlDocumentAttribute attribute => GetXmlNodeNodes(contentMembers[0], attribute, patch, ref errorMessage),
#pragma warning restore CS0618
            PrefabExtensionInsertPatch.PrefabExtensionXmlNodeAttribute attribute => GetXmlNodeNodes(contentMembers[0], attribute, patch, ref errorMessage),
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
    /// Extracts XML nodes from a member annotated with <see cref="PrefabExtensionInsertPatch.PrefabExtensionXmlNodeAttribute"/>
    /// or <see cref="PrefabExtensionInsertPatch.PrefabExtensionXmlDocumentAttribute"/>.
    /// </summary>
    private static IEnumerable<XmlNode>? GetXmlNodeNodes(MemberInfo contentMemberInfo,
        PrefabExtensionInsertPatch.PrefabExtensionSingleContentAttribute attribute,
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
            if (document.DocumentElement is not { } documentElement)
            {
                errorMessage += "is an XML document without a root element.";
                return null;
            }

            xmlNode = documentElement;
        }

        return attribute.RemoveRootNode ? xmlNode.ChildNodes.Cast<XmlNode>() : new List<XmlNode> { xmlNode };
    }

    /// <summary>
    /// Extracts XML nodes from a member annotated with <see cref="PrefabExtensionInsertPatch.PrefabExtensionXmlNodesAttribute"/>.
    /// </summary>
    // ReSharper disable once UnusedParameter.Local
    private static IEnumerable<XmlNode>? GetNodes(MemberInfo contentMemberInfo,
        PrefabExtensionInsertPatch.PrefabExtensionXmlNodesAttribute attribute,
        PrefabExtensionInsertPatch instance,
        ref string errorMessage)
    {
        if (!TryGetContent(contentMemberInfo, instance, ref errorMessage, out IEnumerable<XmlNode>? xmlNodes))
        {
            return null;
        }

        var result = xmlNodes.ToArray();

        // Catches potential issue where XmlDocuments cannot be imported into other documents.
        for (var i = 0; i < result.Length; i++)
        {
            if (result[i] is not XmlDocument document)
            {
                continue;
            }

            if (document.DocumentElement is not { } documentElement)
            {
                errorMessage += "contains an XML document without a root element.";
                return null;
            }

            result[i] = documentElement;
        }

        return result;
    }

    /// <summary>
    /// Parses and extracts XML nodes from a member annotated with <see cref="PrefabExtensionInsertPatch.PrefabExtensionTextAttribute"/>.
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

        if (document.DocumentElement is not { } documentElement)
        {
            errorMessage += "is an XML document without a root element.";
            return null;
        }

        return attribute.RemoveRootNode ? documentElement.ChildNodes.Cast<XmlNode>() : new List<XmlNode> { documentElement };
    }

    /// <summary>
    /// Loads and extracts XML nodes from a file specified by a member annotated with <see cref="PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute"/>.
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

            var moduleInfo = ModuleInfoHelper.LoadFromId(_moduleName);
            if (moduleInfo is null)
            {
                errorMessage += $"Module {_moduleName} is not found.";
                return null;
            }
            var moduleDirectoryPath = Path.Combine(ModuleInfoHelper.GetModulePath(moduleInfo), "GUI");
            var files = Directory.GetFiles(moduleDirectoryPath, "*.xml", SearchOption.AllDirectories);
            files = [.. files.Where(x => string.Equals(Path.GetFileNameWithoutExtension(x), fileName, StringComparison.InvariantCultureIgnoreCase))];
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

        if (document.DocumentElement is not { } documentElement)
        {
            errorMessage += "is an XML document without a root element.";
            return null;
        }

        return attribute.RemoveRootNode ? documentElement.ChildNodes.Cast<XmlNode>() : new List<XmlNode> { documentElement };
    }

    /// <summary>
    /// Evaluates the member specified by <paramref name="memberInfo"/>, validating that its value is assignable to <typeparamref name="T"/>.
    /// </summary>
    private static bool TryGetContent<T>(MemberInfo memberInfo,
        PrefabExtensionInsertPatch instance,
        ref string errorMessage,
        [NotNullWhen(true)] out T? output)
    {
        output = default;

        var value = GetFunction(typeof(T), instance, memberInfo)();
        if (value is null)
        {
            var memberType = memberInfo is PropertyInfo propertyInfo ? propertyInfo.PropertyType : ((MethodInfo) memberInfo).ReturnType;
            errorMessage += $"is of type: {memberType.Name}. A Member flagged with a Content attribute must be " +
                            $"of one of the types listed in {nameof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute)}";
            return false;
        }

        if (value is not T castContent)
        {
            var memberType = memberInfo is PropertyInfo propertyInfo ? propertyInfo.PropertyType : ((MethodInfo) memberInfo).ReturnType;
            errorMessage += $"is of type: {memberType.Name}, while its attribute type expects a {typeof(T).Name}. " +
                            $"See {nameof(PrefabExtensionInsertPatch.PrefabExtensionContentAttribute)} for more information.";
            return false;
        }

        errorMessage = "";
        output = castContent;

        return true;
    }

    /// <summary>
    /// Registers an attribute modification patch for the specified movie and XPath target.
    /// </summary>
    public void RegisterPatch(string movie, string? xpath, PrefabExtensionSetAttributePatch patch) =>
        RegisterPatch(movie, xpath, patch.GetType(), node => SetAttributes(node, patch.Attributes));

    /// <summary>
    /// Sets or updates the specified <paramref name="attributes"/> on the target <see cref="XmlNode"/>.
    /// </summary>
    private static void SetAttributes(XmlNode node, IEnumerable<PrefabExtensionSetAttributePatch.Attribute> attributes)
    {
        if (node.OwnerDocument is not { } ownerDocument)
        {
            return;
        }

        if (node.NodeType != XmlNodeType.Element)
        {
            return;
        }

        if (node.Attributes is not { } nodeAttributes)
        {
            return;
        }

        foreach (var attribute in attributes)
        {
            var nodeAttribute = nodeAttributes[attribute.Name] ?? nodeAttributes.Append(ownerDocument.CreateAttribute(attribute.Name));
            nodeAttribute.Value = attribute.Value;
        }
    }

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

        if (methodInfo is null)
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