# API v1 (Legacy Prefabs)

The **v1 Prefabs API** (`Bannerlord.UIExtenderEx.Prefabs`) is the original prefab modification system in UIExtenderEx. While it remains fully supported for backward compatibility, it is considered **legacy**. New mods and updates should use the modern [v2 Prefab API](../v2/Overview.md).

> [!NOTE]
> For instructions on initializing and registering UIExtenderEx in your mod submodule, see [Getting Started: Registering Your Mod](../general/Overview.md#getting-started-registering-your-mod). Mod registration is identical regardless of whether you use v1 or v2 prefab patches.

---

## Core Concepts

In Gauntlet's Model-View-ViewModel (MVVM) architecture, **prefabs** represent the **View** (declaring the visual hierarchy, layout, styling, and data bindings). Data and command callbacks originate from the **ViewModel**, which you can extend using [ViewModel Mixins](../v2/ViewModelMixin.md).

A v1 prefab extension modifies a movie's underlying XML document before Gauntlet constructs the visual tree.

### The `[PrefabExtension]` Attribute

Every v1 patch class must inherit from an [`IPrefabPatch`](xref:Bannerlord.UIExtenderEx.Prefabs.IPrefabPatch) base class and be decorated with the [`[PrefabExtension]`](xref:Bannerlord.UIExtenderEx.Attributes.PrefabExtensionAttribute) attribute:

```csharp
[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children")]
internal class MyOptionsPatch : PrefabExtensionInsertPatch
{
    // ...
}
```

* **Movie Name (Parameter 1):** The name of the target Gauntlet Movie XML file (for example, `"Options"` or `"Inventory"`).
* **XPath (Parameter 2):** An XPath 1.0 query selecting the target node inside the movie's XML document.
* **Multiple Attributes:** You can attach multiple `[PrefabExtension]` attributes to a single patch class if the same patch logic applies to multiple movies.

### Patch Execution Rules

* **Single Match Rule:** The XPath expression is evaluated against the movie XML, and only the **first** matching node is patched. If the XPath matches nothing, the patch is skipped, an error is logged, and a red warning message is displayed in-game.
* **Parameterless Constructor:** Patch classes must have a `public` parameterless constructor. UIExtenderEx instantiates each patch once during registration (`Extender.Register(...)`).
* **Patch `Id`:** Every v1 patch requires an `Id` property. UIExtenderEx does not enforce uniqueness for this value, but providing a descriptive name is helpful for debugging and logging.
* **Returning XML (`GetPrefabExtension()`):** Structural patches implement `GetPrefabExtension()`, which returns an `XmlDocument`. UIExtenderEx extracts the document's root element (`DocumentElement`) and injects it into the target movie. Any XML comments inside the returned document are stripped.

---

## Patch Types

### 1. Inserting Child Elements (`PrefabExtensionInsertPatch`)

> [!WARNING]
> `PrefabExtensionInsertPatch` and its helper subclasses in `Bannerlord.UIExtenderEx.Prefabs` are marked obsolete. Use [PrefabsV2 `PrefabExtensionInsertPatch`](../v2/PrefabExtensionInsertPatch.md) instead.

[`PrefabExtensionInsertPatch`](xref:Bannerlord.UIExtenderEx.Prefabs.PrefabExtensionInsertPatch) inserts the root element returned by `GetPrefabExtension()` as a child of the XML node selected by your XPath query.

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.Xml;

[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children")]
internal class TestInsertPatch : PrefabExtensionInsertPatch
{
    public override string Id => "TestInsertPatch";

    // Inserts the new element after child index 3 (making it the 5th child)
    public override int Position => 3;

    private XmlDocument XmlDocument { get; } = new();

    public TestInsertPatch()
    {
        XmlDocument.LoadXml("<OptionsTabToggle Id=\"CustomTab\" />");
    }

    public override XmlDocument GetPrefabExtension() => XmlDocument;
}
```

#### Understanding the `Position` Property

In v1, `Position` places the new element **after** the child node at that 0-based index:

| `Position` Value | Placement Behavior |
| :--- | :--- |
| `0` (`PositionFirst`) | Inserts **after** child 0, placing the new element at index 1 (the **second** child). |
| `3` | Inserts **after** child 3, placing the new element at index 4 (the **fifth** child). |
| `PositionLast` (`int.MaxValue`) | Appends the element after the last child. |
| Negative values | Clamped to `0` (inserts after child 0). |

> [!IMPORTANT]
> **v1 Insertion Limitation:** Because `Position = 0` places the new element *after* child 0, v1 cannot insert a node before existing child 0 (at index 0) when the target container already has children. If the container is completely empty, the new node becomes the only child regardless of `Position`.
> 
> To insert an element at the very beginning (index 0) of an existing list of children, you must use the [v2 `PrefabExtensionInsertPatch`](../v2/PrefabExtensionInsertPatch.md) with `InsertType.Child` and `Index = 0`.

#### Built-in File and Resource Helpers

Instead of manually loading XML in the constructor, v1 provides two specialized subclasses:

* **`ModulePrefabExtensionInsertPatch`:** Loads an XML file from your module directory (`Modules/<ModuleName>/GUI/PrefabExtensions/<Name>.xml`):

  ```csharp
  [PrefabExtension("Options", "descendant::ListPanel/Children")]
  internal class FileBasedPatch : ModulePrefabExtensionInsertPatch
  {
      public FileBasedPatch() : base("MyInsertedToggle", "MyModuleName") { }

      public override string Id => "MyInsertedToggle";
      public override int Position => PositionLast;
  }
  ```

* **`EmbedPrefabExtensionInsertPatch`:** Loads an XML document embedded as an assembly manifest resource:

  ```csharp
  [PrefabExtension("Options", "descendant::ListPanel/Children")]
  internal class EmbeddedResourcePatch : EmbedPrefabExtensionInsertPatch
  {
      public EmbeddedResourcePatch() : base(typeof(EmbeddedResourcePatch).Assembly, "MyMod.Resources.MyToggle.xml") { }

      public override string Id => "MyEmbeddedToggle";
      public override int Position => PositionLast;
  }
  ```

---

### 2. Replacing an Element (`PrefabExtensionReplacePatch`)

[`PrefabExtensionReplacePatch`](xref:Bannerlord.UIExtenderEx.Prefabs.PrefabExtensionReplacePatch) replaces the target node selected by the XPath query (including all of its existing children) with the root element returned by `GetPrefabExtension()`.

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.Xml;

[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='Replace']")]
internal class TestReplacePatch : PrefabExtensionReplacePatch
{
    public override string Id => "TestReplacePatch";
    private XmlDocument XmlDocument { get; } = new();

    public TestReplacePatch()
    {
        XmlDocument.LoadXml("<OptionsTabToggle Id=\"ReplacementTab\" Text=\"@ModifiedText\" />");
    }

    public override XmlDocument GetPrefabExtension() => XmlDocument;
}
```

---

### 3. Inserting Sibling Elements (`PrefabExtensionInsertAsSiblingPatch`)

[`PrefabExtensionInsertAsSiblingPatch`](xref:Bannerlord.UIExtenderEx.Prefabs.PrefabExtensionInsertAsSiblingPatch) inserts the root element returned by `GetPrefabExtension()` directly before or after the node selected by your XPath query.

Placement is controlled by the `Type` property:
* `InsertType.Append` (default): Inserts immediately **after** the target node.
* `InsertType.Prepend`: Inserts immediately **before** the target node.

#### Example: Inserting After a Target Node (`Append`)

```csharp
[PrefabExtension("Options", "descendant::OptionsTabToggle[@Id='Video']")]
internal class AppendSiblingPatch : PrefabExtensionInsertAsSiblingPatch
{
    public override string Id => "AppendSiblingPatch";
    public override InsertType Type => InsertType.Append;

    private XmlDocument XmlDocument { get; } = new();

    public AppendSiblingPatch()
    {
        XmlDocument.LoadXml("<OptionsTabToggle Id=\"CustomAfterVideoTab\" />");
    }

    public override XmlDocument GetPrefabExtension() => XmlDocument;
}
```

#### Example: Inserting Before a Target Node (`Prepend`)

```csharp
[PrefabExtension("Options", "descendant::OptionsTabToggle[@Id='Video']")]
internal class PrependSiblingPatch : PrefabExtensionInsertAsSiblingPatch
{
    public override string Id => "PrependSiblingPatch";
    public override InsertType Type => InsertType.Prepend;

    private XmlDocument XmlDocument { get; } = new();

    public PrependSiblingPatch()
    {
        XmlDocument.LoadXml("<OptionsTabToggle Id=\"CustomBeforeVideoTab\" />");
    }

    public override XmlDocument GetPrefabExtension() => XmlDocument;
}
```

---

### 4. Setting Node Attributes (`PrefabExtensionSetAttributePatch`)

[`PrefabExtensionSetAttributePatch`](xref:Bannerlord.UIExtenderEx.Prefabs.PrefabExtensionSetAttributePatch) adds a single attribute to the XML node selected by the XPath query, or overwrites its value if the attribute is already present.

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;

[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='Performance']")]
internal class SetAttributePatch : PrefabExtensionSetAttributePatch
{
    public override string Id => "SetAttributePatch";
    public override string Attribute => "IsVisible";
    public override string Value => "false";
}
```

> [!NOTE]
> A v1 `PrefabExtensionSetAttributePatch` can only set one attribute. To set multiple attributes on a node, you must define multiple patch classes or use the [v2 `PrefabExtensionSetAttributePatch`](../v2/PrefabExtensionSetAttributePatch.md), which supports multiple attributes within a single patch.

---

### 5. Direct XML Manipulation (`CustomPatch<T>`)

[`CustomPatch<T>`](xref:Bannerlord.UIExtenderEx.Prefabs.CustomPatch`1) allows you to directly manipulate the XML object model via C# code. This is useful when the declarative patch types cannot express your required changes.

There are two generic specializations:

1. **`CustomPatch<XmlNode>`:** Targets the specific node matched by the XPath query and passes it to `Apply(XmlNode node)`.
2. **`CustomPatch<XmlDocument>`:** Targets the **entire movie document** and passes it to `Apply(XmlDocument document)`. The XPath parameter in `[PrefabExtension]` is ignored. This is the only patch type capable of modifying multiple nodes across the document at once.

#### Example: Bulk Modification with `CustomPatch<XmlDocument>`

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.Xml;

[PrefabExtension("Options")]
internal class HideAllTabsCustomPatch : CustomPatch<XmlDocument>
{
    public override string Id => "HideAllTabsCustomPatch";

    public override void Apply(XmlDocument document)
    {
        // Select every tab toggle across the document and hide it
        if (document.SelectNodes("descendant::OptionsTabToggle") is { } tabs)
        {
            foreach (XmlElement tab in tabs)
            {
                tab.SetAttribute("IsVisible", "false");
            }
        }
    }
}
```

> [!NOTE]
> Because `CustomPatch` executes arbitrary C# code at runtime, the [Roslyn Analyzers](../general/Analyzers.md) cannot inspect what it changes. However, on any movie where a `CustomPatch` is registered, the analyzers will suppress warnings about missing nodes for your other patches, assuming the custom patch might have introduced them.

---

## Compile-Time Validation (Analyzers)

The [Bannerlord.UIExtenderEx.Analyzers](../general/Analyzers.md) package validates v1 patches at compile time alongside v2 patches:
* **XPath Queries:** Verifies whether XPath expressions match existing elements in base game prefabs.
* **Attributes:** Checks whether attributes set by `PrefabExtensionSetAttributePatch` are valid for the target widget.
* **Inserted XML:** Parses and checks XML passed as string literals to `XmlDocument.LoadXml(...)`, as well as files referenced by `ModulePrefabExtensionInsertPatch` and `EmbedPrefabExtensionInsertPatch`.
* **Prefab Linking:** Validates a patch's XML against the ViewModel and mixins linked via [`[assembly: PrefabLink]`](../v2/PrefabLink.md).

---

## Migrating from v1 to v2

The v2 API simplifies and consolidates the patch types while providing more granular control over XML placement.

### Feature Comparison

| Feature | v1 Approach | v2 Modern Equivalent |
| :--- | :--- | :--- |
| **Insert as Child** | `PrefabExtensionInsertPatch` (places node *after* child index) | [`PrefabExtensionInsertPatch`](../v2/PrefabExtensionInsertPatch.md) with `InsertType.Child` and 0-based `Index` |
| **Insert at Beginning (Index 0)** | ❌ Not supported if children exist | ✅ Supported (`InsertType.Child` with `Index = 0`) |
| **Insert as Sibling** | `PrefabExtensionInsertAsSiblingPatch` (`Append` / `Prepend`) | `PrefabExtensionInsertPatch` with `InsertType.Append` or `InsertType.Prepend` |
| **Replace Node** | `PrefabExtensionReplacePatch` | `PrefabExtensionInsertPatch` with `InsertType.Replace` |
| **Replace While Keeping Children** | ❌ Not supported | ✅ `PrefabExtensionInsertPatch` with `InsertType.ReplaceKeepChildren` |
| **Remove Node** | ❌ Not supported | ✅ `PrefabExtensionInsertPatch` with `InsertType.Remove` |
| **Set Multiple Attributes** | Requires multiple patch classes | ✅ Single [`PrefabExtensionSetAttributePatch`](../v2/PrefabExtensionSetAttributePatch.md) |
| **Load External File** | `ModulePrefabExtensionInsertPatch` | `[PrefabExtensionFileName]` attribute on a patch member |
| **Load Resource Stream** | `EmbedPrefabExtensionInsertPatch` | `[PrefabExtensionText]` attribute reading manifest stream |
| **Direct XML Code Manipulation** | `CustomPatch<T>` | Keep using `CustomPatch<T>` (fully supported) |

### Migration Example: Insert Patch

#### In v1 (Legacy):
```csharp
[PrefabExtension("Options", "descendant::ListPanel/Children")]
internal class OldInsertPatch : PrefabExtensionInsertPatch
{
    public override string Id => "OldInsertPatch";
    public override int Position => 2; // Inserts after child 2 -> index 3
    private XmlDocument Doc { get; } = new();

    public OldInsertPatch() => Doc.LoadXml("<OptionsTabToggle Id=\"Custom\" />");
    public override XmlDocument GetPrefabExtension() => Doc;
}
```

#### In v2 (Modern):
```csharp
[PrefabExtension("Options", "descendant::ListPanel/Children")]
internal class NewInsertPatch : PrefabExtensionInsertPatch
{
    // Explicit placement type and exact 0-based target index
    public override InsertType Type => InsertType.Child;
    public override int Index => 3;

    // Direct string or XML property; no manual XmlDocument boilerplate required
    [PrefabExtensionText]
    public string Content => "<OptionsTabToggle Id=\"Custom\" />";
}
```

For more details on the modern API, see the [v2 API Overview](../v2/Overview.md) and [v2 Prefab Extension Insert Patch Guide](../v2/PrefabExtensionInsertPatch.md).
