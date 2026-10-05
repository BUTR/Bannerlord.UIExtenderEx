# API v2 Overview

The **v2 Prefabs API** (`Bannerlord.UIExtenderEx.Prefabs2`) is the standard, modern API for modifying Gauntlet UI prefabs in *Mount & Blade II: Bannerlord*. It unifies and streamlines the legacy v1 prefab classes into two powerful, flexible patch types:

* **[`PrefabExtensionInsertPatch`](PrefabExtensionInsertPatch.md):** Performs structural XML transformations—inserting children, prepending or appending siblings, replacing nodes (optionally preserving child widgets), or removing nodes.
* **[`PrefabExtensionSetAttributePatch`](PrefabExtensionSetAttributePatch.md):** Adds or overwrites one or more attributes on existing XML nodes.

> [!NOTE]
> To register and enable UIExtenderEx in your mod submodule, see [Getting Started: Registering Your Mod](../general/Overview.md#getting-started-registering-your-mod). If you are maintaining legacy patches, refer to the [API v1 (Legacy) Documentation](../v1/Overview.md).

---

## Gauntlet MVVM Architecture

In TaleWorlds' Gauntlet UI framework, UI screens follow the **Model-View-ViewModel (MVVM)** pattern:

* **View (Prefabs):** XML files that declare the widget hierarchy, visual styling, layout constraints, and data bindings (`@PropertyName` and `@MethodName`). Prefab patches modify this View layer.
* **ViewModel:** C# classes providing the reactive properties and commands bound by the View. In UIExtenderEx, you extend existing game ViewModels using [ViewModel Mixins](ViewModelMixin.md).

For a complete architectural breakdown, see the [General Overview](../general/Overview.md).

---

## Anatomy of a Prefab Patch

Every v2 prefab patch is a C# class derived from either [`PrefabExtensionInsertPatch`](PrefabExtensionInsertPatch.md) or [`PrefabExtensionSetAttributePatch`](PrefabExtensionSetAttributePatch.md) and decorated with the [`[PrefabExtension]`](xref:Bannerlord.UIExtenderEx.Attributes.PrefabExtensionAttribute) attribute:

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children")]
internal sealed class MyOptionsPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    [PrefabExtensionText]
    public string Content => "<Widget Id=\"MyCustomContainer\" />";
}
```

### The `[PrefabExtension]` Attribute

The attribute configures which movie to patch and where inside the XML to apply your changes:

* **Movie Name (Parameter 1):** The name of the target Gauntlet Movie XML file without the `.xml` extension (for example, `"Options"`, `"Inventory"`, or `"Clan"`).
* **XPath (Parameter 2):** An XPath 1.0 query targeting the specific XML element to modify.
* **Multiple Movies:** You can attach multiple `[PrefabExtension]` attributes to a single patch class if the exact same modification applies across multiple screens.

---

## How Patches Are Applied

Understanding the execution lifecycle helps avoid common pitfalls:

1. **Single Match Rule:** The XPath expression is evaluated against the entire target movie XML document. UIExtenderEx patches only the **first** node that matches.
   * If an XPath matches zero nodes, the patch is skipped, an error is logged, and a red in-game message is shown.
   * The [Analyzers](../general/Analyzers.md) check your XPaths at compile time, reporting warnings if an XPath matches nothing (`UIX0020`) or multiple nodes (`UIX0021`).
2. **XPath Is Mandatory in v2:** Although the attribute constructor allows omitting the XPath parameter for legacy v1 compatibility, all v2 patches require a valid XPath. A v2 patch without an XPath will fail when the movie is patched.
3. **Parameterless Constructor:** Patch classes must have a `public` parameterless constructor. UIExtenderEx instantiates each patch once when your mod calls `Extender.Register(...)`.
4. **Registration and Execution Order:** Patches apply in the order they were registered. If multiple mods target the same movie, their patches execute in the order each mod invoked `Extender.Register(...)`. This does not always match module load order: for example, a mod that registers late during `OnBeforeInitialModuleScreenSetAsRoot` (such as MCM) will apply its patches after a mod that registered earlier during `OnSubModuleLoad`, even if the first mod loaded earlier in the module list. See [SubModule Initialization Order](../general/InteractingWithOtherMods.md#submodule-initialization-order).
5. **Dynamic Control:** Patches take effect when you call `Extender.Enable()`. You can also dynamically enable or disable specific patches at runtime using `Extender.Enable(typeof(MyPatch))` and `Extender.Disable(typeof(MyPatch))`. When toggled, affected movies are invalidated and rebuilt the next time they are opened. See [Interacting With Other Mods](../general/InteractingWithOtherMods.md#controlling-patches-and-mixins).

---

## Debugging and Inspecting Patched XML

When building complex prefab extensions, it is helpful to inspect the final XML generated after all patches have been applied.

You can enable XML dumping by setting `DumpXML` to `true` in your UIExtenderEx configuration. When enabled, UIExtenderEx writes the patched XML of every modified movie to:

```text
Modules/Bannerlord.UIExtenderEx/Dumps/<MovieName>_<ModuleName>.xml
```

For more details on runtime diagnostics and settings, see [Settings & Diagnostics](../general/CompiledPrefabs.md#settings).

---

## Best Practices for Writing XPath Selectors

Writing robust XPath expressions ensures your patches remain compatible across game updates and alongside other mods:

* **Avoid brittle absolute paths:** Prefer `descendant::WidgetType[@Id='SpecificId']` over deep absolute paths like `/Prefab/Window/OptionsScreenWidget/Children/...`. If TaleWorlds rearranges intermediate container widgets, an absolute path will break.
* **Filter by unique attributes:** Target nodes using identifying attributes such as `[@Id='TargetId']` or `[@DataSource='{TargetBinding}']`.
* **Select the container for child insertions:** When using `InsertType.Child`, ensure your XPath targets the container node (typically `<Children>`), not the widget itself.

**Helpful XPath Resources:**
* [W3Schools XPath Tutorial](https://www.w3schools.com/xml/xpath_intro.asp)
* [Devhints XPath Cheatsheet](https://devhints.io/xpath)

---

## Where to Go Next

* **[PrefabExtensionInsertPatch Guide](PrefabExtensionInsertPatch.md):** Master insertion types (`Child`, `Append`, `Prepend`, `Replace`, `ReplaceKeepChildren`, `Remove`) and content providers (`[PrefabExtensionText]`, `[PrefabExtensionFileName]`, `[PrefabExtensionXmlNode]`).
* **[PrefabExtensionSetAttributePatch Guide](PrefabExtensionSetAttributePatch.md):** Learn how to add or update widget attributes dynamically.
* **[ViewModel Mixins Guide](ViewModelMixin.md):** Connect your prefab widgets to reactive C# properties, commands, and refresh logic.
* **[Prefab Links Guide](PrefabLink.md):** Statically link prefab patches to ViewModels and Mixins for compile-time validation by Roslyn analyzers.
* **[Worked Examples](Examples.md):** Explore real-world open-source mods utilizing UIExtenderEx.

