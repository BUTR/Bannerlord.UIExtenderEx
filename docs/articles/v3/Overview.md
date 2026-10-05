# UIExtenderEx 3.0 Overview & Upgrade Guide

**UIExtenderEx 3.0** is a major update delivering substantial performance gains, developer-focused diagnostics, and modern C# hooks that eliminate the need for Harmony patches in ViewModel extensions.

Despite the major version bump, **UIExtenderEx 3.0 maintains full backward compatibility** with the modern [v2 Prefabs API](../v2/Overview.md) and [`BaseViewModelMixin<T>`](../v2/ViewModelMixin.md). Mods built against UIExtenderEx 2.13 run seamlessly on 3.0 without code changes.

---

## What's New in 3.0

### 1. Compiled Prefabs Engine
Bannerlord achieves fast UI loading by compiling vanilla XML prefabs into native C# classes ahead of time. UIExtenderEx 3.0 brings this performance advantage to modded interfaces:
* Injected XML patches are dynamically compiled into optimized C# assemblies on the fly.
* Screens with extensive mod extensions load with near-vanilla performance, bypassing runtime XML parsing.
* Learn more in [Compiled Prefabs Deep Dive](../general/CompiledPrefabs.md).

### 2. Built-in Mixin Hooks (Zero Harmony Required)
Previously, mods often had to combine UIExtenderEx mixins with Harmony patches whenever they needed to intercept ViewModel methods, access private fields, or react to property updates. Version 3.0 introduces native hooks directly within [`BaseViewModelMixin<T>`](xref:Bannerlord.UIExtenderEx.ViewModels.BaseViewModelMixin`1):
* **Method Overrides ([`[BUTRViewModelOverride]`](Mixins.md#taking-over-a-viewmodel-method)):** Intercept or replace vanilla ViewModel methods across all callers (UI buttons, hotkeys, and internal game logic), with support for calling the original implementation.
* **Fast Private Member Access ([`[BUTRUnsafeAccessor]`](Mixins.md#reaching-private-members)):** Read or write private fields and invoke private methods using zero-overhead IL stubs that are verified at registration time.
* **Universal Property Notifications ([`OnViewModelPropertyChanged`](Mixins.md#hearing-every-notification)):** Catch all property changes dispatched by the target ViewModel—including typed value notifications (`PropertyChangedWithValue`) that vanilla `PropertyChanged` handlers miss.
* **Safe External Mixin Retrieval ([`ViewModelMixins.Get`](Mixins.md#reaching-a-mixin-from-outside)):** Retrieve the active mixin instance attached to any ViewModel without relying on static singletons or weak-reference properties.

### 3. Smarter Registration and Error Reporting
Registration in 3.0 provides actionable diagnostics directly in your logs:
* Detects and logs property or method name collisions between mixins and target ViewModels.
* Gracefully handles partial assembly loads. If a mod contains a mixin targeting an uninstalled DLC or missing mod dependency, UIExtenderEx registers all available types and logs a clean warning rather than crashing `Extender.Register(...)`. See [What registration reports](Mixins.md#what-registration-reports).
* Identifies unresolvable accessor stubs and warns when abstract mixins lack `handleDerived: true`.

### 4. Roslyn Analyzers and Static Linking
The [Bannerlord.UIExtenderEx.Analyzers](../general/Analyzers.md) package provides real-time compile-time checking inside Visual Studio and Rider. Using [`[assembly: PrefabLink]`](../v2/PrefabLink.md), the analyzer validates your XML bindings, attributes, and XPath expressions against your C# ViewModel and mixin definitions before you launch the game.

---

## Real-World Case Studies

To demonstrate how 3.0 simplifies real-world mod codebases, explore our worked examples:

* **[Worked Example: MCM](MCM.md):** Demonstrates how the Mod Configuration Menu replaced four distinct Harmony patches, eliminated custom delegate management, and cleaned up options handling by adopting 3.0 mixin hooks.
* **[Worked Example: Diplomacy](Diplomacy.md):** Illustrates how a large mod (70+ members across 7 ViewModels) cleaned up redundant refresh cycles, simplified property change listening, and unified shared mixin logic using base classes.

---

## Upgrading from UIExtenderEx 2.x

### Supported Game Versions
UIExtenderEx 3.0 targets **Mount & Blade II: Bannerlord v1.3.4 and later**. Mods targeting older legacy game releases should continue using UIExtenderEx 2.13.

#### Why v1.3.4?

While UIExtenderEx 2.x supported game releases dating back to v1.0.0, version 3.0 establishes **v1.3.4** as its new baseline. This requirement is driven by breaking Gauntlet UI API changes introduced in Bannerlord v1.3:

* **Visual Definition Easing Overhaul (Primary Reason):**  
  In v1.3, TaleWorlds redesigned animation interpolation on `VisualDefinition`. The legacy constructor accepting a simple `bool easeIn` flag was removed in favor of a new five-argument constructor requiring `EaseType`, `EaseFunction`, and `AnimationInterpolation`. Because the [Compiled Prefabs](../general/CompiledPrefabs.md) code generator emits strongly-typed C# constructors directly into generated prefab classes, the generated code can only compile against the new engine API.
* **Direct Engine Calls (Zero Reflection):**  
  UIExtenderEx 3.0 replaces reflection with direct, zero-overhead API calls wherever possible. When registering dynamically loaded widget classes, it invokes `WidgetInfo.Refresh()`. In pre-v1.3 versions, this method was named `WidgetInfo.Reload()`. While 2.x bridged this gap using reflection, 3.0 targets the modern method directly.

| Engine API | Pre-v1.3 | v1.3 and Later | Impact in 3.0 |
| :--- | :--- | :--- | :--- |
| **Visual Definitions** | `VisualDefinition(..., bool easeIn)` | `VisualDefinition(..., EaseType, EaseFunction)` | Code generator emits the new 5-parameter constructor for animated widgets. |
| **Widget Table Refresh** | `WidgetInfo.Reload()` | `WidgetInfo.Refresh()` | Invoked directly without reflection when registering dynamic widget types. |

> [!NOTE]
> Versions v1.3.0 through v1.3.2 were beta-only releases; **v1.3.4** is the first official stable game release that includes all of these API updates.

### Automatic Behavioral Changes (No Code Changes Required)

1. **Compiled Prefabs by Default:** Your prefab patches are compiled into executable C# code. Ensure all patch XML fragments loaded via `[PrefabExtensionFileName]` are located outside `GUI/Prefabs` (e.g. `GUI/PrefabExtensions/`). See [Keep Patch Fragments Out of GUI/Prefabs](../general/CompiledPrefabs.md#keep-patch-fragments-out-of-guiprefabs).
2. **Abstract Mixin Registration:** In 2.x, a mixin declared for an abstract ViewModel without `handleDerived: true` could never match an instance, but incurred construction overhead. In 3.0, UIExtenderEx skips registering it and logs a warning. Set `[ViewModelMixin(handleDerived: true)]` if you intend to extend derived subclasses.
3. **Resilient Assembly Scanning:** Missing dependencies or uninstalled DLC types no longer abort the entire registration process. Valid patches and mixins continue to register normally.
4. **Standalone Remove Patches:** In 3.0, a `PrefabExtensionInsertPatch` with `InsertType.Remove` no longer requires a dummy content property.

### Recommended Code Improvements

While existing 2.x code continues to function, modernizing your codebase with 3.0 features is strongly recommended:

| Legacy 2.x Pattern | Modern 3.0 Alternative | Benefit |
| :--- | :--- | :--- |
| Harmony prefix/postfix on ViewModel methods | [`[BUTRViewModelOverride]`](Mixins.md#taking-over-a-viewmodel-method) | Removes Harmony dependency; cleanly scoped to mixin lifecycle. |
| Harmony postfix on property setters or manual event handler | [`OnViewModelPropertyChanged`](Mixins.md#hearing-every-notification) | Catches all 9 typed property notification overloads automatically. |
| `AccessTools.FieldRefAccess` or `GetPrivate<T>` | [`[BUTRUnsafeAccessor]`](Mixins.md#reaching-private-members) | Zero reflection overhead; checked at startup during registration. |
| Storing static "current instance" or weak reference on VM | [`ViewModelMixins.Get<T>()`](Mixins.md#reaching-a-mixin-from-outside) | Safe, strongly-typed retrieval without static leaks or name collisions. |
| Calling `OnRefresh()` manually inside mixin constructor | Rely on automatic refresh forwarding | Prevents running expensive refresh logic twice on screen load. (See [The Refresh Rule](Mixins.md#the-refresh-rule)). |
| `[PrefabExtensionXmlDocument]` | `[PrefabExtensionXmlNode]` | Cleaner API; handles both `XmlDocument` and `XmlNode`. |
