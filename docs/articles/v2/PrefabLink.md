# Prefab Links

In UIExtenderEx, UI extensions are decoupled into two distinct layers:
* A **[Prefab Patch](PrefabExtensionInsertPatch.md)** that injects or modifies Gauntlet XML widgets (the View).
* A **[ViewModel Mixin](ViewModelMixin.md)** that injects properties and commands into the screen's ViewModel.

Because XML files and C# classes are separate, the C# compiler cannot naturally verify whether the property names in your XML match the members declared on your ViewModel or mixin. 

The `[assembly: PrefabLink]` attribute bridges this gap: it explicitly informs the [Roslyn Analyzers](../general/Analyzers.md) which ViewModel and mixin provide the data context for your prefab patch or custom prefab XML.

---

## Anatomy of `[assembly: PrefabLink]`

Assembly-level attributes can be placed in any C# file within your project. A common best practice is to group all links into a single file (such as `UILinks.cs`), providing a centralized manifest of every screen modified by your mod.

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

// Links a prefab patch to its target ViewModel and specific Mixin
[assembly: PrefabLink(typeof(ModOptionsPagePatch), typeof(OptionsVM), typeof(OptionsVMMixin))]

// Links a standalone custom prefab to its root ViewModel data context
[assembly: PrefabLink("ModOptionsView_MCM", typeof(ModOptionsVM))]

namespace MyMod;

[ViewModelMixin]
internal sealed class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
{
    public OptionsVMMixin(OptionsVM vm) : base(vm) { }

    [DataSourceProperty]
    public ModOptionsVM ModOptions { get; } = new();

    [DataSourceProperty]
    public int PanelWidth { get; set; }
}

[PrefabExtension("Options", "descendant::Widget[@Id='OptionsPanel']/Children")]
internal sealed class ModOptionsPagePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    [PrefabExtensionText]
    public string Content => "<ModOptionsView_MCM DataSource=\"{ModOptions}\" Width=\"@PanelWidth\" />";
}
```

---

## Attribute Parameters

The `[assembly: PrefabLink]` attribute provides flexible overloads depending on whether you are linking a prefab patch or a custom standalone prefab:

| Parameter | Type | Description |
| :--- | :--- | :--- |
| **Patch or Prefab** | `Type` or `string` | **For patches (`Type`):** The C# patch class decorated with `[PrefabExtension]` (inheriting from `PrefabExtensionInsertPatch`, `PrefabExtensionSetAttributePatch`, or a v1 patch).<br/>**For custom prefabs (`string`):** The name of your custom prefab registered via `WidgetFactoryManager.CreateAndRegister` or the XML file name. |
| **Target ViewModel** | `Type` | The `ViewModel` type acting as the data context at the target insertion point. For prefab patches, this is the ViewModel active at the node matched by the XPath. For custom prefabs, this is the ViewModel at the prefab's root. |
| **Target Mixin** *(Optional)* | `Type` | The specific `[ViewModelMixin]` class providing custom properties or commands to the XML. The mixin must be attached to the target ViewModel (either directly targeting it or targeting an inherited base class with `handleDerived: true`). |

> [!NOTE]
> If a single prefab patch binds members from multiple mixins attached to the same ViewModel, you can define multiple `[assembly: PrefabLink]` attributes for that patch, one for each mixin.

---

## How Roslyn Analyzers Use Prefab Links

`[assembly: PrefabLink]` exists purely as compile-time metadata. It has **zero runtime overhead** and does not change how UIExtenderEx loads or executes patches in the game.

When you build your mod, the [Bannerlord.UIExtenderEx.Analyzers](../general/Analyzers.md) package inspects these attributes to perform comprehensive validation:

* **Binding Verification (`UIX0015`):** Checks every `@PropertyName` binding and command callback in the injected XML against the declared ViewModel and mixin. If a property is misspelled or missing, the build emits a warning or error.
* **Link Integrity (`UIX0018`):** Verifies that the specified patch or prefab exists, that the ViewModel inherits from `TaleWorlds.Library.ViewModel`, and that the mixin is actually configured to attach to that ViewModel.
* **Stale Link Detection (`UIX0019`):** Ensures that the XML actually binds at least one member from the specified mixin. If a refactor removes the bindings, the analyzer alerts you that the link is obsolete.

---

## Automatic Inference vs. Explicit Links

In many cases, the Roslyn analyzer can automatically infer which ViewModel a patch binds by scanning your mixins for matching property names. 

However, writing explicit `[assembly: PrefabLink]` annotations is recommended in the following scenarios:

1. **Patches Binding Only Native Properties:** If your injected XML binds exclusively to vanilla game properties on the base ViewModel (without using any custom mixin members), the analyzer cannot infer the ViewModel and emits `UIX0016`. An explicit link resolves this immediately.
2. **Ambiguous Mixin Bindings:** If multiple mixins define properties with identical names (such as `IsEnabled` or `Title`), automated inference might choose the wrong ViewModel. An explicit link guarantees accurate validation.
3. **Custom Standalone Prefabs:** Custom prefabs loaded dynamically by game code (such as custom panels or dialog windows) cannot be traced automatically by the analyzer. Adding a string-based prefab link enables full compile-time checking of the custom prefab XML.
4. **Refactoring Safety:** Explicit links guarantee that if you rename a mixin property or move a patch to a different screen, the compiler immediately flags broken bindings rather than silently guessing.

---

## Version Compatibility

* **UIExtenderEx 3.0+:** The `PrefabLinkAttribute` is natively declared within the core assembly (`Bannerlord.UIExtenderEx.Attributes`).
* **UIExtenderEx 2.x:** When using the analyzer package with UIExtenderEx 2.x, the analyzer automatically injects the attribute definition as an internal type into your compilation, leaving no residual footprint in your final binary.

Your `[assembly: PrefabLink]` declarations remain fully source-compatible across both versions.

