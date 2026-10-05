# UIExtenderEx Documentation

Welcome to the official documentation for **UIExtenderEx**, the UI extension and runtime optimization framework for *Mount & Blade II: Bannerlord*.

UIExtenderEx allows multiple mods to cleanly extend, customize, and inject visual UI elements and reactive business logic into TaleWorlds' **Gauntlet UI** system—without conflicting with other mods or destructively overwriting base game XML files.

---

## Overview

### For Players
UIExtenderEx is a shared dependency framework required by mods that modify or extend the user interface. It provides no standalone visual changes on its own.

### Installation & Module Load Order
In your game launcher (or mod manager such as Novus or Vortex), place `Bannerlord.UIExtenderEx` near the top of the module load order, directly after **`Bannerlord.Harmony`** or **`Bannerlord.ButterLib`**:

```text
1. Bannerlord.Harmony
2. Bannerlord.ButterLib
3. Bannerlord.UIExtenderEx
4. Native / SandBoxCore / ... (Official Game Modules)
5. Mod Modules...
```

---

## Key Features

* **🧩 Non-Destructive Prefab Extensions (View):** Add new widgets, modify attributes, or replace/remove elements across vanilla screens using targeted XPath queries ([API v2 Guide](articles/v2/Overview.md)).
* **🔄 Reactive ViewModel Mixins (ViewModel):** Dynamically attach custom properties (`[DataSourceProperty]`), UI commands (`[DataSourceMethod]`), and lifecycle callbacks to native game ViewModels without subclassing ([ViewModel Mixins](articles/v2/ViewModelMixin.md)).
* **⚡ Compiled Prefabs Engine (3.0+):** Maintains vanilla Bannerlord's fast pre-compiled UI loading speeds. Patched XML trees are dynamically compiled into optimized C# assemblies on the fly and cached, eliminating runtime XML parsing stutters ([Compiled Prefabs](articles/general/CompiledPrefabs.md)).
* **🪝 Native 3.0 Mixin Hooks (Zero Harmony Required):** Intercept and override game ViewModel methods across all callers ([`[BUTRViewModelOverride]`](articles/v3/Mixins.md#taking-over-a-viewmodel-method)), access private fields with zero-overhead IL stubs ([`[BUTRUnsafeAccessor]`](articles/v3/Mixins.md#reaching-private-members)), and capture all nine typed property notification variants ([`OnViewModelPropertyChanged`](articles/v3/Mixins.md#hearing-every-notification)).
* **🛡️ Roslyn Compile-Time Analyzers:** Real-time IDE diagnostics and automated code fixes ([`UIX0001`–`UIX0030`](articles/general/Analyzers.md)) in Visual Studio and JetBrains Rider to catch invalid XPaths, mistyped bindings, and multi-game-version incompatibilities before launching the game.

---

## Quick Start

### 1. Add NuGet References

Install `Bannerlord.UIExtenderEx` and the companion analyzer package in your mod's `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Bannerlord.UIExtenderEx" Version="3.0.*" />
  <PackageReference Include="Bannerlord.UIExtenderEx.Analyzers" Version="3.0.*" PrivateAssets="all" />
</ItemGroup>
```

### 2. Register Your Extender

Initialize and enable your extender inside your `MBSubModuleBase` class:

```csharp
using Bannerlord.UIExtenderEx;
using TaleWorlds.MountAndBlade;

public class SubModule : MBSubModuleBase
{
    private static readonly UIExtender Extender = UIExtender.Create("MyModUniqueId");

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        // Scan this assembly for [PrefabExtension] and [ViewModelMixin] classes
        Extender.Register(typeof(SubModule).Assembly);
        Extender.Enable();
    }
}
```

### 3. Inject a Widget & Bind Logic

Create a prefab patch to inject your visual widget and a ViewModel mixin to provide data:

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

// 1. Prefab Patch: Insert custom button into the Options screen
[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children")]
internal sealed class MyOptionsButtonPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    [PrefabExtensionText]
    public string Content => """
        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
                      SuggestedWidth="200" SuggestedHeight="40"
                      Command.Click="@OnCustomButtonClicked">
          <Children>
            <TextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren"
                        Text="@CustomButtonLabel" />
          </Children>
        </ButtonWidget>
        """;
}

// 2. ViewModel Mixin: Provide data and click handler
[ViewModelMixin]
internal sealed class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
{
    public OptionsVMMixin(OptionsVM vm) : base(vm) { }

    [DataSourceProperty]
    public string CustomButtonLabel => "Click Me!";

    [DataSourceMethod]
    public void OnCustomButtonClicked()
    {
        InformationManager.DisplayMessage(new InformationMessage("Custom button clicked!"));
    }
}
```

---

## Pre-Compiled Prefabs & Configuration

TaleWorlds optimizes UI loading speeds by compiling vanilla XML prefabs into C# assemblies ahead of time (AutoGens). In versions prior to 3.0, UIExtenderEx forced all screens to parse XML dynamically, introducing UI load stutter.

In UIExtenderEx 3.0+:
* **Untouched screens** execute TaleWorlds' native pre-compiled C# classes.
* **Patched screens** compile on the fly in the background via an embedded, isolated Roslyn compiler and cache into `Modules/Bannerlord.UIExtenderEx/CompiledPrefabs`. Subsequent opens use the fast compiled variant directly.

### Settings Configuration

Settings can be adjusted in the `<Settings>` section of `SubModule.xml` or configured in-game via the [Mod Configuration Menu (MCM)](https://github.com/Aragas/Bannerlord.MBOptionScreen):

| Setting | Default | Effect |
| :--- | :--- | :--- |
| `CompiledPrefabs` | `true` | Enables compiling patched movies to C# at runtime. If `false`, patched movies always load via XML fallback. |
| `RecordTimings` | `false` | Writes movie load, compile, and warm-up timings to `CompiledPrefabs/Timings/`, one session file at a time. For investigating load times. |
| `DumpGeneratedCode` | `false` | Writes generated C# source code files to `CompiledPrefabs/Sources/` for inspection. |
| `DumpXML` | `false` | Writes the final patched XML document of every modified movie to `Modules/Bannerlord.UIExtenderEx/Dumps/`. |
| `DisableGeneratedPrefabs` | `false` | Reverts to pre-v3.0 behavior: disables all pre-compiled prefabs across the game, forcing every movie through the XML parser. Useful only for diagnostic debugging. |

---

## Documentation Portals

| Portal | Description |
| :--- | :--- |
| **[General Articles](articles/general/Overview.md)** | Core MVVM concepts, multi-mod coordination, compiled prefabs introduction, and Roslyn analyzer rules. |
| **[UIExtenderEx 3.0 Guide](articles/v3/Overview.md)** | What's new in 3.0, migration steps from 2.x, and real-world case studies ([MCM](articles/v3/MCM.md), [Diplomacy](articles/v3/Diplomacy.md)). |
| **[API v2 Guide](articles/v2/Overview.md)** | Guide to modern prefab patches (`InsertPatch`, `SetAttributePatch`), mixins, and compile-time `[PrefabLink]`. |
| **[API v1 Guide (Legacy)](articles/v1/Overview.md)** | Reference for the legacy v1 prefab API and how to upgrade to modern v2 patches. |
| **[Core Internals](articles/runtime/Overview.md)** | Architecture overview, execution lifecycles, global Harmony hooks, and thread-safety models. |
| **[Compiled Prefabs Deep Dive](articles/compiled-prefabs/Overview.md)** | In-depth documentation on Roslyn code generation, caching pipeline, XML loader parity, and test oracles. |
| **[API Reference](xref:Bannerlord.UIExtenderEx)** | Complete class, interface, and attribute documentation generated directly from source code XML doc comments. |

---

## Community & Resources

* **GitHub Repository:** [BUTR/Bannerlord.UIExtenderEx](https://github.com/BUTR/Bannerlord.UIExtenderEx)
* **NexusMods:** [UIExtenderEx on NexusMods](https://www.nexusmods.com/mountandblade2bannerlord/mods/2102)
* **Steam Workshop:** [UIExtenderEx on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2859222409)
* **Official Modding Documentation:** [moddocs.bannerlord.com](https://moddocs.bannerlord.com/)
* **Community Modding Documentation:** [docs.bannerlordmodding.lt](https://docs.bannerlordmodding.lt/)
* **BUTR Discord:** [Join the Bannerlord Modding Community](https://discord.gg/banu-sarrana)
