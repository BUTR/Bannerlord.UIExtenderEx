# Documentation Articles

Explore the complete catalog of articles, guides, case studies, and architecture references for **UIExtenderEx**.

---

## 🚀 General Guides

Fundamental concepts for extending Bannerlord's UI, integrating with the broader modding ecosystem, and setting up build-time validation.

* **[Overview](general/Overview.md):** Introduction to Gauntlet UI's Model-View-ViewModel (MVVM) architecture, the two core pillars (Prefabs and Mixins), and getting started.
* **[Interacting with Other Mods](general/InteractingWithOtherMods.md):** Locating other mods' extenders via `UIExtender.GetUIExtenderFor`, toggling patches dynamically, and managing SubModule execution order.
* **[Compiled Prefabs](general/CompiledPrefabs.md):** User and mod author guide to the in-memory compiled prefabs system, runtime settings, cache folder layout, and troubleshooting.
* **[Analyzers](general/Analyzers.md):** Full reference for the `Bannerlord.UIExtenderEx.Analyzers` package, documenting rules `UIX0001` through `UIX0030`, multi-version validation against `types.json`, and automated IDE code fixes.

---

## ⚡ UIExtenderEx 3.0

Everything new in version 3.0, including backward compatibility details, new mixin hooks, and deep-dive migration case studies.

* **[3.0 Overview & Upgrade Guide](v3/Overview.md):** Summary of 3.0 features, supported game versions, automatic behavioral upgrades, and recommended patterns.
* **[Mixin Hooks](v3/Mixins.md):** Native C# hooks replacing Harmony patches:
  * [`[BUTRViewModelOverride]`](v3/Mixins.md#taking-over-a-viewmodel-method): Intercepting ViewModel methods across all callers.
  * [`[BUTRUnsafeAccessor]`](v3/Mixins.md#reaching-private-members): High-speed IL accessors for private fields and methods.
  * [`OnViewModelPropertyChanged`](v3/Mixins.md#hearing-every-notification): Catching all 9 typed property notification overloads.
  * [`ViewModelMixins.Get`](v3/Mixins.md#reaching-a-mixin-from-outside): Safe external mixin retrieval from ViewModel instances.
  * [The Refresh Rule](v3/Mixins.md#the-refresh-rule): Understanding constructor refresh forwarding.
* **[Worked Example: Mod Configuration Menu (MCM)](v3/MCM.md):** Step-by-step walkthrough of how MCM replaced four Harmony patches, reflection delegates, and dummy properties using 3.0 mixin hooks.
* **[Worked Example: Diplomacy](v3/Diplomacy.md):** Real-world case study on optimizing a large additive mod (70+ members across 7 ViewModels) by eliminating redundant refresh cycles and unifying shared mixins.

---

## 🛠️ API v2 Guide

The modern, recommended API for modifying Gauntlet UI prefabs and attaching ViewModel mixins.

* **[API v2 Overview](v2/Overview.md):** The modern prefab extension architecture, anatomy of a patch, execution rules, and XPath best practices.
* **[PrefabExtensionInsertPatch](v2/PrefabExtensionInsertPatch.md):** Structural XML transformations (`Child`, `Prepend`, `Append`, `Replace`, `ReplaceKeepChildren`, `Remove`) and supplying content via `[PrefabExtensionText]`, `[PrefabExtensionFileName]`, or `[PrefabExtensionXmlNode]`.
* **[PrefabExtensionSetAttributePatch](v2/PrefabExtensionSetAttributePatch.md):** Adding new XML attributes or updating values on existing widgets.
* **[ViewModel Mixins](v2/ViewModelMixin.md):** Deriving from `BaseViewModelMixin<T>`, defining `[DataSourceProperty]` and `[DataSourceMethod]`, raising property notifications, and lifecycle cleanup via `OnFinalize`.
* **[Prefab Links](v2/PrefabLink.md):** Using `[assembly: PrefabLink]` to statically link XML patches and custom prefabs to ViewModels and Mixins for compile-time validation.
* **[Worked Examples](v2/Examples.md):** Complete end-to-end starter project blueprint and featured open-source references.

---

## 📦 API v1 (Legacy)

Documentation for the legacy v1 prefab extension system and instructions for upgrading existing code.

* **[API v1 (Legacy Prefabs)](v1/Overview.md):** Reference documentation for `PrefabExtensionInsertPatch`, `PrefabExtensionReplacePatch`, and migration mappings to the modern v2 API.

---

## 🔍 Core Architecture & Internals

Written for framework maintainers and advanced developers interested in the engine plumbing and hosting layer.

* **[Core Architecture](runtime/Overview.md):** Global Harmony patches (`GauntletMoviePatch`, `WidgetPrefabPatch`), dynamic mixin patches, the `Runtimes` hosting seam (`IPrefabRuntime`), and thread-safety models.
* **[Lifecycle & Startup](runtime/Lifecycle.md):** Step-by-step execution timeline from SubModule load order validation, runtime installation, mod registration, enabling/disabling, to teardown and retired mixins.
* **[Prefab Patching](runtime/PrefabPatching.md):** The detailed XML patching pipeline, dynamic runtime registrations (`WidgetFactoryManager`, `BrushFactoryManager`), and engine XML loader bug fixes (`ParsePatch`, `WidgetExtensionsPatch`, `WidgetTemplatePatch`).
* **[ViewModel Patching](runtime/ViewModelPatching.md):** Member injection, flat binding table architecture, method override chains, unsafe accessor transpilation, and retired mixin finalization.

---

## ⚙️ Compiled Prefabs Internals

Comprehensive documentation on UIExtenderEx's in-memory C# compilation engine, code generator, and verification suites.

* **[Compiled Prefabs Architecture](compiled-prefabs/Overview.md):** Problem definition, modern per-load evaluation, runtime assembly split, and thread model.
* **[Compilation Pipeline](compiled-prefabs/Pipeline.md):** The complete decision tree of `GauntletMovie.Load`, `GamePrefabRuntime` declination rules, fingerprint calculation (`PrefabFingerprint`), and dependency tracking (`PrefabDependencies`).
* **[Code Generator](compiled-prefabs/Generator.md):** Emitting C# classes from XML, member resolution priority, mixin receivers, and widget fast-assignment paths.
* **[Handling XML Differences](compiled-prefabs/HandlingDifferences.md):** Principles, governance rules, and triage process for discrepancies between compiled prefabs and the runtime XML loader.
* **[Deviations from the XML Loader](compiled-prefabs/XmlDeviations.md):** Authoritative catalog of all 11 approved behavioral differences from the XML loader and their accompanying analyzer guards.
* **[In-Memory Compilation](compiled-prefabs/Compilation.md):** Embedding Roslyn via ILRepack, preserving SIMD acceleration (`System.Numerics.Vectors`), dynamic binding redirects, and off-thread assembly loading.
* **[Testing & Verification Strategy](compiled-prefabs/Testing.md):** Test project structure, automated test oracles (`LoaderOracle`, `BindingLoaderOracle`), memory ceilings, and benchmarks.
