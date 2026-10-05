# Overview

**UIExtenderEx** is an extension framework for *Mount & Blade II: Bannerlord* that enables mod developers to cleanly extend, customize, and inject UI elements and logic into the game's Gauntlet UI system without conflicting with other mods.

In Bannerlord, user interface screens and widgets are tightly bound to the game's internal data sources. Without a dedicated framework, modifying an existing screen often requires overwriting base game XML files or writing fragile, invasive runtime patches. UIExtenderEx provides a structured, non-destructive architecture that allows multiple mods to modify the same screens simultaneously.

---

## Gauntlet UI and the MVVM Pattern

TaleWorlds' Gauntlet UI framework is built around the **Model-View-ViewModel (MVVM)** architectural pattern, separating visual presentation from game logic:

* **Model (Game State):** Represents the underlying game state, entities, and campaign systems (such as `Hero`, `Settlement`, `Clan`, or campaign behaviors).
* **ViewModel (Presentation Logic):** C# classes inheriting from `TaleWorlds.Library.ViewModel` that expose reactive properties (annotated with `[DataSourceProperty]`) and command callbacks (annotated with `[DataSourceMethod]`) for the UI.
* **View (Visual Layout):** Gauntlet Movie XML prefabs declaring the widget hierarchy, visual styling, layout constraints, and data bindings (`@PropertyName`).

UIExtenderEx provides dedicated extension mechanisms for the **View** and the **ViewModel**:

| Layer | In Bannerlord | How UIExtenderEx Extends It |
| :--- | :--- | :--- |
| **Model** | Core game state (`Hero`, `Settlement`, `Clan`, campaign behaviors) | *Not extended directly.* Mixins read and modify game state through the game's standard APIs. |
| **ViewModel** | `ViewModel` subclasses with `[DataSourceProperty]` and `[DataSourceMethod]` members | **[ViewModel Mixins](../v2/ViewModelMixin.md):** `[ViewModelMixin]` classes derived from `BaseViewModelMixin<TViewModel>`. |
| **View** | Prefabs: Movie XML templates and widget definitions | **[Prefab Extensions](../v2/Overview.md):** `[PrefabExtension]` patch classes that insert, set, or modify XML nodes. |

---

## The Two Core Pillars

Modifying a Bannerlord screen typically requires two coordinated parts:
1. **Injecting visual widgets** into a screen's prefab (the View).
2. **Providing data and event handlers** for those widgets in the screen's ViewModel.

### 1. Prefabs are the View

In Gauntlet, a **prefab** is an XML file defining the visual elements of a screen or UI component: the widget hierarchy, layout properties, brush styles, and data bindings (using `@Name`). Prefabs contain no business logic of their own. A **movie** is a top-level prefab loaded by a screen with a specific `ViewModel` instance as its data context.

A **Prefab Extension** modifies this XML structure before Gauntlet constructs the movie. Using targeted XPath selectors, your mod can:
* Insert new widgets (such as buttons, text labels, lists, or custom widgets).
* Modify or set attributes on existing widgets (such as changing colors, sizes, or visibility bindings).
* Replace or remove existing XML nodes.

Any widget you insert can bind to any property or method on the screen's ViewModel—including custom members contributed by your mixins.

> [!NOTE]
> Even with **[Compiled Prefabs](CompiledPrefabs.md)**—where Gauntlet runs ahead-of-time compiled C# classes instead of parsing XML at runtime—UIExtenderEx compiles patched XML into optimized C# assemblies on the fly, ensuring your extensions work seamlessly without sacrificing performance.

### 2. Mixins Extend the ViewModel

Because the game engine instantiates its own `ViewModel` classes directly, mods cannot simply substitute a derived subclass. **ViewModel Mixins** solve this challenge:

* A mixin class attaches automatically to every runtime instance of its target `ViewModel`.
* Properties marked with `[DataSourceProperty]` and methods marked with `[DataSourceMethod]` on your mixin are exposed as if they were native members of that `ViewModel`.
* Injected XML widgets bind to these mixin members using standard `@Name` syntax, exactly like vanilla properties.
* Inside the mixin, `base.ViewModel` provides direct access to the target game `ViewModel` instance, allowing you to inspect game data, execute actions, and notify the UI of state changes using `OnPropertyChanged()`.

### 3. How the View and ViewModel Connect

A typical UI modification connects both halves through **data binding**:
* A **Prefab Extension** adds a widget (for example, a button bound via `Command.Click="@OnCustomButtonClicked"` and `Text="@CustomButtonLabel"`).
* A **ViewModel Mixin** targeting that screen's ViewModel defines `OnCustomButtonClicked()` and `CustomButtonLabel`.
* The binding names (`@OnCustomButtonClicked`, `@CustomButtonLabel`) form the contract linking your visual widgets to your C# logic.

---

## Getting Started: Registering Your Mod

To use UIExtenderEx, create a `UIExtender` instance in your mod's `MBSubModuleBase` class, register your assembly containing your patches and mixins, and enable the extender:

```csharp
using Bannerlord.UIExtenderEx;
using TaleWorlds.MountAndBlade;

public class SubModule : MBSubModuleBase
{
    // Create an extender instance identified by a unique name for your mod
    private static readonly UIExtender Extender = UIExtender.Create("MyModId");

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        // Register all mixins and prefab patches defined in this assembly
        Extender.Register(typeof(SubModule).Assembly);

        // Enable the registered extensions
        Extender.Enable();
    }
}
```

* **Unique Identifier:** The name passed to `UIExtender.Create(...)` uniquely identifies your mod's extender. This name allows [other mods](InteractingWithOtherMods.md) to query, enable, or disable your extensions if cross-mod compatibility requires it.
* **Assembly Registration:** Calling `Extender.Register(...)` scans the assembly for classes decorated with `[PrefabExtension]` or `[ViewModelMixin]` and prepares them for application.
* **Enabling:** Calling `Extender.Enable()` activates your patches so Gauntlet incorporates them whenever affected screens load.

---

## Advanced Capabilities

Beyond basic prefab patching and mixin properties, UIExtenderEx provides several advanced tools:

* **ViewModel Method Overrides (`[BUTRViewModelOverride]`):**  
  Intercept or replace an existing method on a game ViewModel. When decorated with `[BUTRViewModelOverride]`, your mixin method replaces the original method for all callers (including TaleWorlds' internal code) and receives a delegate to invoke the original implementation. See [Mixin Hooks](../v3/Mixins.md#taking-over-a-viewmodel-method).

* **Fast Private Member Access (`[BUTRUnsafeAccessor]`):**  
  Access private fields or methods on TaleWorlds game types without reflection overhead. Define a `static` stub method annotated with `[BUTRUnsafeAccessor]`, and UIExtenderEx replaces its body with high-speed IL accessors during registration. See [Mixin Hooks](../v3/Mixins.md#reaching-private-members).

* **Compiled Prefabs:**  
  Bannerlord optimizes UI loading speeds by compiling vanilla prefabs to C# ahead of time. UIExtenderEx compiles patched XML into C# classes at runtime, providing near-vanilla load times for modded screens. See [Compiled Prefabs](CompiledPrefabs.md).

* **Runtime Widget and Prefab Registration:**  
  Register custom C# widget classes or dynamic XML prefabs created in code at runtime through `WidgetFactoryManager`. See [Widget classes and prefabs registered at runtime](CompiledPrefabs.md#widget-classes-and-prefabs-registered-at-runtime).

* **Roslyn Analyzers (`Bannerlord.UIExtenderEx.Analyzers`):**  
  A dedicated analyzer package that verifies your XPath queries, mixin signatures, and binding targets against base game prefabs at compile time, catching errors before you launch the game. See [Analyzers](Analyzers.md).

---

## Where to Go Next

* **[UIExtenderEx 3.0](../v3/Overview.md):** What 3.0 adds, how to upgrade from 2.x, and worked examples from MCM and Diplomacy.
* **[API v2 Guide](../v2/Overview.md):** The modern API for writing prefab extension patches (`PrefabExtensionInsertPatch` and `PrefabExtensionSetAttributePatch`).
* **[ViewModel Mixins Guide](../v2/ViewModelMixin.md):** Detailed guide on creating mixins, handling property change notifications, and accessing target ViewModels.
* **[Interacting With Other Mods](InteractingWithOtherMods.md):** Learn how to selectively enable, disable, or coordinate with extensions registered by other mods.
* **[Compiled Prefabs Deep Dive](CompiledPrefabs.md):** Technical details on how UIExtenderEx compiles and caches patched prefabs, along with troubleshooting and configuration options.
* **[Analyzer Diagnostics Reference](Analyzers.md):** Complete catalog of compile-time diagnostic rules (`UIX0001`–`UIX0030`) and quick fixes.
* **[API v1 Guide (Legacy)](../v1/Overview.md):** Reference documentation for the legacy v1 prefab patch system.
* **[Architecture & Internals](../runtime/Overview.md):** In-depth documentation on the internal structure and design of UIExtenderEx.
