# Worked Example: Mod Configuration Menu (MCM)

The [Mod Configuration Menu (MCM)](https://github.com/Aragas/Bannerlord.MBOptionScreen) is one of the most widely used UI mods in *Mount & Blade II: Bannerlord*, providing a unified settings menu for the modding community directly within the game's native **Options** screen.

MCM's implementation on `OptionsVM` is the definitive real-world demonstration of why UIExtenderEx 3.0's mixin hooks were created: in UIExtenderEx 2.x, achieving this integration required an invasive set of Harmony patches, reverse patches, reflection delegates, and dummy properties. In 3.0, **all of the mixin's Harmony patches were removed** and replaced with clean, native mixin hooks. (MCM still patches the options screens for other reasons, such as blocking tab switches while a search is active.)

---

## The Prefab Patches

MCM injects its custom widgets into the native `Options` movie. Because prefab patching semantics did not break between 2.x and 3.0, these patches remain essentially identical:

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using System.Collections.Generic;

// 1. Inserts the "Mod Options" tab toggle button into the top tab bar
[PrefabExtension("Options", "descendant::ListPanel[@Id='TabToggleList']/Children/OptionsTabToggle[5]")]
internal sealed class OptionsTabTogglePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Prepend;

    [PrefabExtensionText]
    public string Content => """
        <OptionsTabToggle DataSource="{ModOptions}"
                          PositionYOffset="2"
                          Parameter.ButtonBrush="Header.Tab.Center"
                          Parameter.TabName="ModOptionsPage" />
        """;
}

// 2. Inserts the settings page view container into the main TabControl
[PrefabExtension("Options", "descendant::TabControl[@Id='TabControl']/Children/*[5]")]
internal sealed class OptionsPagePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Prepend;

    [PrefabExtensionText]
    public string Content => "<ModOptionsPageView Id=\"ModOptionsPage\" DataSource=\"{ModOptions}\" />";
}

// 3. Dynamically binds the description panel width so MCM can hide it when Mod Options is active
[PrefabExtension("Options", "descendant::Widget[@Id='DescriptionsRightPanel']")]
internal sealed class OptionsDescriptionPanelPatch : PrefabExtensionSetAttributePatch
{
    public override List<Attribute> Attributes => [new("SuggestedWidth", "@DescriptionWidth")];
}
```

---

## Architectural Comparison: 2.x vs. 3.0

### The Legacy 2.x Architecture

In UIExtenderEx 2.x, a mixin could only expose new properties and methods. Any deeper integration with the host screen required external patching:

* **Private Category List:** MCM had to use `AccessTools2.FieldRefAccess` reflection delegates to insert its `ModOptions` ViewModel into the private `List<ViewModel> _categories` field of `OptionsVM`.
* **Tab Switch Notifications:** TaleWorlds updates the active tab using a typed event for `CategoryIndex`. Because standard `PropertyChanged` events do not receive typed notifications, MCM had to maintain a Harmony postfix on the `CategoryIndex` property setter.
* **Saving & Canceling:** MCM needed to intercept Done and Cancel actions. Standard mixin methods only redirected clicks on UI buttons; keyboard shortcuts (like Escape or Enter) and controller shortcuts bypassed the mixin entirely. MCM was forced to use Harmony postfixes, reverse patches, and delegate forwarding.
* **Static Context Leaks:** Because Harmony postfixes are static, MCM had to add a dummy `WeakReference<OptionsVMMixin>` property named `MCMMixin` to `OptionsVM` so static patches could find the active mixin instance. A second one, `ModOptionsSelected`, carried the selected-tab state from the `CategoryIndex` postfix back into the mixin.

In total, 2.x required a dedicated Harmony instance, 1 reverse patch, 3 postfixes, 3 reflection delegates (`ExecuteDone`, `ExecuteCancel` and the `_categories` field), and 2 dummy properties on `OptionsVM`.

---

### The Modern 3.0 Architecture

With UIExtenderEx 3.0, all of this boilerplate is replaced by native mixin hooks directly inside `OptionsVMMixin`:

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

namespace MCM.UI.UIExtenderEx;

[ViewModelMixin(nameof(OptionsVM.RefreshValues))]
internal sealed class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
{
    // High-speed, zero-reflection accessor for the private _categories field
    [BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_categories")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ref List<ViewModel> Categories(OptionsVM instance) => throw new NotImplementedException();

    private int _descriptionWidth = 650;

    [DataSourceProperty]
    public ModOptionsVM ModOptions { get; } = new();

    [DataSourceProperty]
    public int DescriptionWidth
    {
        get => _descriptionWidth;
        private set => SetField(ref _descriptionWidth, value);
    }

    public OptionsVMMixin(OptionsVM vm) : base(vm)
    {
        // Insert custom category directly into the private collection, at the index its tab has on screen:
        // the prefab patches put the MCM tab before GameKey, the fifth tab
        Categories(vm).Insert(4, ModOptions);
    }

    // Called automatically after OptionsVM.RefreshValues finishes executing
    public override void OnRefresh() => ModOptions.RefreshValues();

    // Catches all typed property changes from OptionsVM (including CategoryIndex)
    protected override void OnViewModelPropertyChanged(string propertyName)
    {
        if (propertyName != nameof(OptionsVM.CategoryIndex) || ViewModel is not { } vm)
            return;

        var isMcmSelected = vm.CategoryIndex == 4;
        ModOptions.IsDisabled = !isMcmSelected;
        DescriptionWidth = isMcmSelected ? 0 : 650;
    }

    // Intercepts Done, Cancel, and Close across ALL callers (UI buttons, hotkeys, Esc key)

    [BUTRViewModelOverride(nameof(OptionsVM.ExecuteDone))]
    private void ExecuteDone(Action original) => ModOptions.ExecuteDoneInternal(false, original);

    [BUTRViewModelOverride(nameof(OptionsVM.ExecuteCancel))]
    private void ExecuteCancel(Action original) => ModOptions.ExecuteCancelInternal(false, original);

    [BUTRViewModelOverride(nameof(OptionsVM.ExecuteCloseOptions))]
    private void ExecuteCloseOptions(Action original)
    {
        ModOptions.ExecuteCancelInternal(false);
        original();
    }
}
```

---

## Key Benefits Achieved

| Challenge | 2.x Solution | 3.0 Modern Solution |
| :--- | :--- | :--- |
| **Access private `_categories`** | `AccessTools2.FieldRefAccess` | [`[BUTRUnsafeAccessor]`](Mixins.md#reaching-private-members) (IL stub validated at startup) |
| **Screen refresh synchronization** | Harmony postfix on `RefreshValues` | `[ViewModelMixin(nameof(OptionsVM.RefreshValues))]` with `OnRefresh()` (see [The refresh rule](Mixins.md#the-refresh-rule)) |
| **Detect tab switching** | Harmony postfix on `CategoryIndex` setter | [`OnViewModelPropertyChanged`](Mixins.md#hearing-every-notification) |
| **Intercept Done & Cancel** | Mixin methods + Harmony postfixes + reverse patches | [`[BUTRViewModelOverride]`](Mixins.md#taking-over-a-viewmodel-method) (covers hotkeys and UI alike) |
| **Locating mixin from patches** | Dummy `MCMMixin` property on `OptionsVM` | Not needed; hooks live directly on the mixin instance |

By migrating to UIExtenderEx 3.0:
* **Zero Harmony Footprint:** MCM no longer patches any methods on `OptionsVM`.
* **Universal Input Coverage:** Keyboard shortcuts (such as the Escape key) and controller inputs cleanly trigger MCM's save and cancel logic.
* **Analyzer Safety:** Roslyn analyzers validate all bindings without false-positive collision warnings (`UIX0001`).
