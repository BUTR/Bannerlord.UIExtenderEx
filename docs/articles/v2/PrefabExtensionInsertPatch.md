# PrefabExtensionInsertPatch

[`PrefabExtensionInsertPatch`](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch) is the primary structural patch type in the v2 API. It allows you to insert new elements, prepend or append siblings, replace existing nodes (with or without preserving their child hierarchy), or remove nodes entirely from a Gauntlet Movie XML document.

The patch's behavior is governed by two key properties:
* **`Type` (`InsertType`):** Determines the transformation applied to the target node.
* **`Index` (`int`):** Controls positional placement for child insertions and child inheritance during replacements.

---

## Insertion Types (`InsertType`)

| `InsertType` | Behavior | Positional Context (`Index`) |
| :--- | :--- | :--- |
| `Child` | Inserts the new XML nodes as children of the target element. | `Index` specifies the 0-based position among existing children. Default is `0` (first child). Values greater than or equal to the current child count append to the end. |
| `Prepend` | Inserts the new XML nodes immediately **before** the target node as adjacent siblings. | `Index` is not used. |
| `Append` | Inserts the new XML nodes immediately **after** the target node as adjacent siblings. | `Index` is not used. |
| `Replace` | Replaces the target node and all of its existing children with the new XML content. | `Index` is not used. |
| `ReplaceKeepChildren` | Replaces the target node with the new XML content, while preserving the original node's children and re-parenting them into the new node. | If inserting multiple nodes, `Index` specifies which newly inserted node inherits the original children. |
| `Remove` | Completely removes the target node and all of its children from the movie. | No content or `Index` required. |

---

## Supplying XML Content

Unless using `InsertType.Remove`, every `PrefabExtensionInsertPatch` must declare **exactly one** public instance property or parameterless method decorated with a content attribute derived from `PrefabExtensionContentAttribute`:

| Attribute | Expected Member Type | Description |
| :--- | :--- | :--- |
| [`[PrefabExtensionText]`](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionTextAttribute) | `string` | An XML snippet string. |
| [`[PrefabExtensionFileName]`](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute) | `string` | The file name of an XML file located in your module's `GUI/` directory. |
| [`[PrefabExtensionXmlNode]`](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionXmlNodeAttribute) | [`XmlNode`](xref:System.Xml.XmlNode) or [`XmlDocument`](xref:System.Xml.XmlDocument) | An in-memory XML node or document. If an `XmlDocument` is provided, its root element (`DocumentElement`) is used. |
| [`[PrefabExtensionXmlNodes]`](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionXmlNodesAttribute) | [`IEnumerable<XmlNode>`](xref:System.Collections.Generic.IEnumerable`1) | A sequence of XML nodes to insert sequentially in order. |

> [!NOTE]
> `[PrefabExtensionXmlDocument]` is obsolete. Use `[PrefabExtensionXmlNode]` instead, which accepts both `XmlDocument` and `XmlNode`.

### Member Visibility and Evaluation

* Content properties or methods must be **`public` instance** members. UIExtenderEx cannot invoke `private`, `protected`, or `static` members.
* The content member is evaluated dynamically each time the movie is patched, not cached permanently.
* All XML comments inside the supplied content, at any depth, are stripped before insertion.
* In UIExtenderEx 3.0+, `InsertType.Remove` patches do not require any content member. (In legacy 2.x versions, a dummy content member was required).

### Stripping Root Nodes with `RemoveRootNode`

When inserting multiple sibling elements, XML syntax normally requires wrapping them in a single root element (for example, `<Root><WidgetA /><WidgetB /></Root>`). 

To avoid inserting an unwanted container widget, pass `true` to the `RemoveRootNode` parameter on the attribute constructor:
* `[PrefabExtensionText(true)]`
* `[PrefabExtensionFileName(true)]`
* `[PrefabExtensionXmlNode(true)]`

When set to `true`, UIExtenderEx discards the wrapper root element and inserts all of its child elements directly.

---

## Examples

### 1. Prepending a Sibling (`InsertType.Prepend`)

Inserts the new widget directly before the target element at the same hierarchy level.

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle")]
internal sealed class PrependTabPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Prepend;

    [PrefabExtensionText]
    public string Content => "<OptionsTabToggle Id=\"CustomPrependedTab\" />";
}
```

```xml
<!-- Before Patch -->
<OptionsScreenWidget Id="Options">
    <Children>
        <OptionsTabToggle Id="Gameplay" />
    </Children>
</OptionsScreenWidget>

<!-- After Patch -->
<OptionsScreenWidget Id="Options">
    <Children>
        <OptionsTabToggle Id="CustomPrependedTab" />
        <OptionsTabToggle Id="Gameplay" />
    </Children>
</OptionsScreenWidget>
```

---

### 2. Appending a Sibling (`InsertType.Append`)

Inserts the new widget directly after the target element at the same hierarchy level.

```csharp
[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle")]
internal sealed class AppendTabPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionText]
    public string Content => "<OptionsTabToggle Id=\"CustomAppendedTab\" />";
}
```

```xml
<!-- Before Patch -->
<OptionsScreenWidget Id="Options">
    <Children>
        <OptionsTabToggle Id="Gameplay" />
    </Children>
</OptionsScreenWidget>

<!-- After Patch -->
<OptionsScreenWidget Id="Options">
    <Children>
        <OptionsTabToggle Id="Gameplay" />
        <OptionsTabToggle Id="CustomAppendedTab" />
    </Children>
</OptionsScreenWidget>
```

---

### 3. Inserting as a Child at an Index (`InsertType.Child`)

Inserts the new element into the target container at a specific 0-based index.

```csharp
[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children")]
internal sealed class InsertChildAtIndexPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    // 0 = first child, 1 = second child, etc.
    // If the index exceeds the child count, the element is appended to the end.
    public override int Index => 1;

    [PrefabExtensionText]
    public string Content => "<OptionsTabToggle Id=\"InsertedSecondTab\" />";
}
```

```xml
<!-- Before Patch -->
<OptionsScreenWidget Id="Options">
    <Children>
        <OptionsTabToggle Id="FirstTab" />
        <OptionsTabToggle Id="ThirdTab" />
    </Children>
</OptionsScreenWidget>

<!-- After Patch -->
<OptionsScreenWidget Id="Options">
    <Children>
        <OptionsTabToggle Id="FirstTab" />
        <OptionsTabToggle Id="InsertedSecondTab" />
        <OptionsTabToggle Id="ThirdTab" />
    </Children>
</OptionsScreenWidget>
```

---

### 4. Replacing an Entire Node (`InsertType.Replace`)

Replaces the target node and all of its descendants with your new widget.

```csharp
[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle[@Id='Audio']")]
internal sealed class ReplaceTabPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Replace;

    [PrefabExtensionText]
    public string Content => "<CustomAudioPanel Id=\"ReplacementAudioPanel\" />";
}
```

```xml
<!-- Before Patch -->
<OptionsScreenWidget Id="Options">
    <Children>
        <OptionsTabToggle Id="Audio">
            <Children>
                <Standard.TopPanel />
            </Children>
        </OptionsTabToggle>
    </Children>
</OptionsScreenWidget>

<!-- After Patch -->
<OptionsScreenWidget Id="Options">
    <Children>
        <CustomAudioPanel Id="ReplacementAudioPanel" />
    </Children>
</OptionsScreenWidget>
```

---

### 5. Replacing a Node While Preserving Children (`InsertType.ReplaceKeepChildren`)

Replaces the target container element with a new widget while preserving all existing child nodes. If inserting multiple new nodes, `Index` designates which new node receives the preserved children.

```csharp
[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/ListPanel")]
internal sealed class SwapContainerKeepChildrenPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.ReplaceKeepChildren;

    // If multiple nodes are inserted, Index specifies which one inherits the children (0 = first node)
    public override int Index => 0;

    [PrefabExtensionText]
    public string Content => "<NavigatableGridWidget Id=\"CustomGrid\" ColumnCount=\"2\" />";
}
```

```xml
<!-- Before Patch -->
<ListPanel Id="OldList">
    <Children>
        <ButtonWidget Id="Button1" />
        <ButtonWidget Id="Button2" />
    </Children>
</ListPanel>

<!-- After Patch -->
<NavigatableGridWidget Id="CustomGrid" ColumnCount="2">
    <Children>
        <ButtonWidget Id="Button1" />
        <ButtonWidget Id="Button2" />
    </Children>
</NavigatableGridWidget>
```

#### Preserving Children with Multi-Node Injection (`RemoveRootNode = true`)

```csharp
[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/ListPanel")]
internal sealed class MultiNodeReplaceKeepChildrenPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.ReplaceKeepChildren;

    // Node at index 1 (<Panel Id="Target">) receives the preserved children
    public override int Index => 1;

    [PrefabExtensionText(removeRootNode: true)]
    public string Content => """
        <Wrapper>
            <Widget Id="Header" />
            <Panel Id="Target" />
            <Widget Id="Footer" />
        </Wrapper>
        """;
}
```

```xml
<!-- After Patch -->
<Widget Id="Header" />
<Panel Id="Target">
    <Children>
        <ButtonWidget Id="Button1" />
        <ButtonWidget Id="Button2" />
    </Children>
</Panel>
<Widget Id="Footer" />
```

---

### 6. Removing a Node (`InsertType.Remove`)

Deletes the target element and its entire subtree from the movie XML.

```csharp
[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle[@Id='UnwantedTab']")]
internal sealed class RemoveTabPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Remove;
}
```

```xml
<!-- Before Patch -->
<OptionsScreenWidget Id="Options">
    <Children>
        <OptionsTabToggle Id="Tab1" />
        <OptionsTabToggle Id="UnwantedTab" />
        <OptionsTabToggle Id="Tab2" />
    </Children>
</OptionsScreenWidget>

<!-- After Patch -->
<OptionsScreenWidget Id="Options">
    <Children>
        <OptionsTabToggle Id="Tab1" />
        <OptionsTabToggle Id="Tab2" />
    </Children>
</OptionsScreenWidget>
```

---

## Loading XML from External Files (`[PrefabExtensionFileName]`)

For large UI templates, keeping XML inside external `.xml` files instead of C# string literals improves syntax highlighting, validation, and maintainability.

```csharp
[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children")]
internal sealed class ExternalFilePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    // Loads GUI/PrefabExtensions/MyCustomContainer.xml from your module folder
    [PrefabExtensionFileName]
    public string FileName => "MyCustomContainer";
}
```

### File Resolution Rules

1. **Folder Location:** Place patch XML files in your module's `GUI/` directory or any subfolder within it (such as `GUI/PrefabExtensions/MyCustomContainer.xml`).
2. **File Extension:** You can include or omit the `.xml` extension in the string (`"MyCustomContainer"` or `"MyCustomContainer.xml"`). Matching is case-insensitive.
3. **Module Resolution:** UIExtenderEx identifies your module using the string identifier provided when calling `UIExtender.Create("MyModuleId")`.
4. **Hot Reloading:** The external file is read from disk each time the movie is patched. You can edit the XML file while the game is running, reload the screen (or call `Extender.Disable(typeof(MyPatch))` followed by `Extender.Enable(typeof(MyPatch))`), and see your changes without restarting the game or recompiling your C# assembly.

> [!CAUTION]
> **Do NOT place patch XML files inside `GUI/Prefabs/`!**
> 
> TaleWorlds' `WidgetFactory` registers every XML file inside `GUI/Prefabs` as a standalone prefab. However, patch fragments are incomplete snippets that use a `<Widget>` or `<DummyRoot>` root rather than `<Prefab>`. While the XML loader ignores unreferenced fragment files, the compiled-prefab generator attempts to parse them, triggering an engine exception that forces any movie referencing the fragment to fall back to XML. Always place patch fragments in a separate folder, such as `GUI/PrefabExtensions/`.
> 
> See [Keep Patch Fragments Out of GUI/Prefabs](../general/CompiledPrefabs.md#keep-patch-fragments-out-of-guiprefabs) for more information.

---

## Compile-Time Checking with Prefab Links

To ensure that injected XML widgets, attributes, and data bindings (`@PropertyName`, `Command.Click="@MethodName"`) are validated at compile time, pair your patch with [`[assembly: PrefabLink]`](PrefabLink.md). The [Roslyn Analyzers](../general/Analyzers.md) will inspect your XML and confirm all bindings against the target ViewModel and its registered [ViewModel Mixins](ViewModelMixin.md).

