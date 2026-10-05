# PrefabExtensionSetAttributePatch

[`PrefabExtensionSetAttributePatch`](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch) is a specialized patch type in the v2 API used to add new XML attributes to an existing element or overwrite the values of existing ones in a Gauntlet Movie.

Unlike the legacy v1 attribute patch (which only allowed modifying a single attribute per class), the v2 patch allows you to modify or declare multiple attributes within a single patch class.

---

## How It Works

A set-attribute patch targets an XML element selected by your XPath query. When applied:
* **Attribute already exists:** Its value is overwritten with the new value you specified.
* **Attribute does not exist:** A new attribute with the specified name and value is created and added to the element.

### Key Rules and Considerations

* **Exact Case Matching:** XML attributes in Gauntlet are strictly case-sensitive. Specifying `isVisible` will not overwrite an existing `IsVisible` attribute—it will create a duplicate attribute with different casing.
* **Target Node Must Be an Element:** The XPath expression in `[PrefabExtension]` must select an `XmlElement`. If the XPath points to an attribute node (such as `.../@Id`) or a text node, the patch is ignored.
* **No Removal:** `PrefabExtensionSetAttributePatch` cannot delete an attribute. If you need to remove an attribute entirely from a vanilla element, replace the node using [`PrefabExtensionInsertPatch`](PrefabExtensionInsertPatch.md) with `InsertType.Replace`.
* **Runtime Evaluation:** The `Attributes` property is evaluated each time the target movie is patched.

---

## Implementation Details

To create an attribute patch:
1. Inherit from [`PrefabExtensionSetAttributePatch`](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch).
2. Decorate the class with [`[PrefabExtension("MovieName", "XPath")]`](xref:Bannerlord.UIExtenderEx.Attributes.PrefabExtensionAttribute).
3. Override the `Attributes` property, returning a `List<Attribute>`. Each item is an instance of the nested `Attribute` struct (`new Attribute(string name, string value)`).

### Example: Modifying Multiple Attributes

The following example locates an `OptionsTabToggle` inside the `"Options"` movie, rebinds its visibility to a ViewModel property, and ensures it is enabled:

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using System.Collections.Generic;

[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle")]
internal sealed class ConfigureOptionsTabPatch : PrefabExtensionSetAttributePatch
{
    public override List<Attribute> Attributes => new()
    {
        new Attribute("IsVisible", "@IsDefaultCraftingMenuVisible"),
        new Attribute("IsEnabled", "true"),
        new Attribute("Brush", "Header.Tab.Toggle")
    };
}
```

#### XML Transformation

**Before patch:**
```xml
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle IsVisible="true" />
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>
```

**After patch:**
```xml
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle IsVisible="@IsDefaultCraftingMenuVisible" IsEnabled="true" Brush="Header.Tab.Toggle" />
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>
```

---

## Linking Attributes to ViewModels

When an attribute value uses Gauntlet data-binding syntax (such as `@PropertyName` or `*ParameterName`):
* The bound property must exist on the screen's active ViewModel or one of its registered [ViewModel Mixins](ViewModelMixin.md).
* You can statically link the patch to the target ViewModel using [`[assembly: PrefabLink]`](PrefabLink.md). This enables the [Roslyn Analyzers](../general/Analyzers.md) to validate attribute names and binding targets at compile time, warning you if a property is renamed or missing.

