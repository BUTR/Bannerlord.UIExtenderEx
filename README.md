# Bannerlord.UIExtenderEx
<p align="center">
  <a href="https://github.com/BUTR/Bannerlord.UIExtenderEx">
    <img src="https://github.com/BUTR/Bannerlord.UIExtenderEx/blob/dev/resources/Butter.png?raw=true" alt="Logo"/>
  </a>
  <br/>
  <a href="https://github.com/BUTR/Bannerlord.UIExtenderEx">
    <img src="https://aschey.tech/tokei/github/BUTR/Bannerlord.UIExtenderEx?category=code" alt="Lines Of Code"/>
  </a>
  <a href="https://www.codefactor.io/repository/github/butr/bannerlord.uiextenderex">
    <img src="https://www.codefactor.io/repository/github/butr/bannerlord.uiextenderex/badge" alt="CodeFactor"/>
  </a>
  <a href="https://codeclimate.com/github/BUTR/Bannerlord.UIExtenderEx/maintainability">
    <img alt="Code Climate maintainability" src="https://img.shields.io/codeclimate/maintainability-percentage/BUTR/Bannerlord.UIExtenderEx">
  </a>
  <a href="https://butr.github.io/Bannerlord.UIExtenderEx">
    <img src="https://img.shields.io/badge/Documentation-%F0%9F%94%8D-blue?style=flat" alt="Documentation"/>
  </a>
  <a href="https://translate.butr.link/engage/bannerlord-uiextenderex/">
    <img src="https://translate.butr.link/widget/bannerlord-uiextenderex/svg-badge.svg" alt="Translation status">
  </a>
  <br/>
  <a href="https://github.com/BUTR/Bannerlord.UIExtenderEx/actions/workflows/test.yml?query=branch%3Adev">
    <img alt="GitHub Workflow Status (event)" src="https://img.shields.io/github/actions/workflow/status/BUTR/Bannerlord.UIExtenderEx/test.yml?branch=dev&label=Game%20Stable%20and%20Beta">
  </a>
  <a href="https://codecov.io/gh/BUTR/Bannerlord.UIExtenderEx">
    <img src="https://codecov.io/gh/BUTR/Bannerlord.UIExtenderEx/branch/dev/graph/badge.svg"  alt="CodeCov"/>
  </a>
  <br/>
  <a href="https://www.nuget.org/packages/Bannerlord.UIExtenderEx">
    <img src="https://img.shields.io/nuget/v/Bannerlord.UIExtenderEx.svg?label=NuGet%20Bannerlord.UIExtenderEx&colorB=blue" alt="NuGet Bannerlord.UIExtenderEx"/>
  </a>
  <a href="https://www.nuget.org/packages/Bannerlord.UIExtenderEx.Analyzers">
    <img src="https://img.shields.io/nuget/v/Bannerlord.UIExtenderEx.Analyzers.svg?label=NuGet%20Bannerlord.UIExtenderEx.Analyzers&colorB=blue" alt="NuGet Bannerlord.UIExtenderEx.Analyzers"/>
  </a>
  <br/>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102">
    <img src="https://img.shields.io/badge/NexusMods-UIExtenderEx-yellow.svg" alt="NexusMods UIExtenderEx"/>
  </a>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="NexusMods UIExtenderEx">
    <img src="https://img.shields.io/endpoint?url=https%3A%2F%2Fnmstats.butr.link%2Fmod-version%3FgameId%3D3174%26modId%3D2102" />
  </a>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="NexusMods UIExtenderEx">
    <img src="https://img.shields.io/endpoint?url=https%3A%2F%2Fnmstats.butr.link%2Fdownloads%3Ftype%3Dunique%26gameId%3D3174%26modId%3D2102" />
  </a>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="NexusMods UIExtenderEx">
    <img src="https://img.shields.io/endpoint?url=https%3A%2F%2Fnmstats.butr.link%2Fdownloads%3Ftype%3Dtotal%26gameId%3D3174%26modId%3D2102" />
  </a>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="NexusMods UIExtenderEx">
    <img src="https://img.shields.io/endpoint?url=https%3A%2F%2Fnmstats.butr.link%2Fdownloads%3Ftype%3Dviews%26gameId%3D3174%26modId%3D2102" />
  </a>
  <br/>
  <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=2859222409">
    <img alt="Steam Mod Configuration Menu" src="https://img.shields.io/badge/Steam-UIExtenderEx-blue.svg" />
  </a>
  <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=2859222409">
    <img alt="Steam Downloads" src="https://img.shields.io/steam/downloads/2859222409?label=Downloads&color=blue">
  </a>
  <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=2859222409">
    <img alt="Steam Views" src="https://img.shields.io/steam/views/2859222409?label=Views&color=blue">
  </a>
  <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=2859222409">
    <img alt="Steam Subscriptions" src="https://img.shields.io/steam/subscriptions/2859222409?label=Subscriptions&color=blue">
  </a>
  <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=2859222409">
    <img alt="Steam Favorites" src="https://img.shields.io/steam/favorites/2859222409?label=Favorites&color=blue">
  </a>
  <br/>
</p>

A library that enables multiple mods to alter standard game interface.  
Previously, a fork of [UIExtenderLib](https://github.com/shdwp/UIExtenderLib) that was de-forked.

## Installation
This module should be one of the highest in loading order. Ideally, it should be loaded after ``Bannerlord.Harmony`` or ``Bannerlord.ButterLib``.

## For Players
This mod is a dependency mod that does not provide anything by itself. You need to additionally install mods that use it.

## Usage
The game's UI follows the Model-View-ViewModel pattern. Prefabs, the movie XML, are the View: UIExtenderEx changes them with prefab extensions. ViewModels supply the data the prefabs bind to: UIExtenderEx extends them with mixins.

Check the [``Articles``](https://butr.github.io/Bannerlord.UIExtenderEx/articles/) section of our documentation!

Add ``Bannerlord.UIExtenderEx.Analyzers`` to your mod to have your mixins and prefab XML checked while you build: members that replace the game's, names two mixins both add, refresh methods that do not exist, mixins that would never run or would crash the screen, and in your XML misspelled attributes, values the loader cannot convert, and bindings to members the ViewModel does not have. Most of them come with a code fix. See [Analyzers](https://butr.github.io/Bannerlord.UIExtenderEx/articles/general/Analyzers.html).

## Pre-compiled prefabs (AutoGens)
The game uses two prefab systems: XML prefabs that are parsed at runtime, and C# prefabs that TaleWorlds pre-compiles from the same XML with `TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.exe`. We call the latter AutoGens.
AutoGens skip the XML parsing and bind to ViewModels with typed code instead of reflection, which is noticeably faster, especially on the `Mono` runtime.

UIExtenderEx patches the XML. The game's AutoGens were built from the unpatched XML, so they would silently ignore every patch. UIExtenderEx used to disable AutoGens globally because of that.

UIExtenderEx now keeps AutoGens enabled and recompiles only the movies a patch affects:
* When a movie is loaded whose prefab tree contains a patched prefab, UIExtenderEx generates C# from the patched XML with a fork of the game's own code generator (`TaleWorlds.GauntletUI.CodeGenerator`, included with TaleWorlds' permission), compiles it on a background thread and registers the result in place of the game's variant. The first load of such a movie still uses XML.
* Compiled prefabs are cached in `Modules/Bannerlord.UIExtenderEx/CompiledPrefabs`. The cache key is a fingerprint of the patched XML, the ViewModel and widget assemblies and the enabled mixins, so the next game session uses them immediately and any change triggers a rebuild. Superseded builds of a movie are deleted when the new one is compiled, and the whole cache is cleared once UIExtenderEx or the game is updated.
* Properties and commands contributed by ViewModel mixins are bound through typed access to the mixin instance, which the stock generator cannot do.
* Enabling or disabling patches makes the game parse the affected prefabs again the next time a movie uses them, instead of reusing the copies it keeps for movies currently open. Open movies keep what they show; the next open gets the patched version, in XML and compiled form alike.
* Widget classes and prefabs that mods register at runtime through `WidgetFactoryManager` are generated like the game's own. A widget type the factory cannot resolve fails generation with its name; the report lands in `CompiledPrefabs/Failed/<Movie>/<ViewModel>/errors.txt`, next to compile failures.
* The compiler ships with UIExtenderEx: Roslyn and every assembly it binds to are ILRepacked into `Bannerlord.UIExtenderEx.Compiler` and internalized. Nothing about it is resolved by name, so no other module's copy of `System.Collections.Immutable` or `System.Reflection.Metadata` can reach it and module load order cannot change what it does. It used to be the Roslyn in the game's `mono` folder, which made UIExtenderEx compete with other modules for those two assemblies.
* While the game loads, a worker thread brings the cached assemblies into the process and warms the compiler up, so the first patched movie pays neither Roslyn's start-up cost nor the assembly load. Loading an assembly costs 90 to 210 ms in a modded game, spent in other mods' assembly-load listeners, which is why freshly compiled assemblies are loaded on the worker too and the main thread only registers them.
* Mixins and mod ViewModels may stay `internal`: the generated code binds to them the way publicizer tools do (`IgnoresAccessChecksTo`).
* The module runs on both runtimes the game ships on, the Mono embedded in the Steam, GOG and Epic builds and the .NET 6 of the Xbox PC / Microsoft Store build. `Bannerlord.UIExtenderEx.dll`, the one assembly mods use, is `netstandard2.0` and serves both; only the compiled-prefab part, which carries the compiler, is built per runtime. The NuGet package holds `Bannerlord.UIExtenderEx.dll` for `netstandard2.0` alone, as it did before 3.0.0.
* A compiled assembly is loaded after the game built its widget type table, so UIExtenderEx adds its widget classes to that table when it registers one, in place. The game itself only offers a full rescan of every loaded assembly for this, which is what UIExtenderEx falls back to if the table cannot be reached.

Settings control this behaviour. They are declared in the `<Settings>` block of `SubModule.xml`, which is also where they are stored: each property has a `Default`, and the current value is a `Value` attribute next to it. Edit it by hand, or let [MCM](https://github.com/Aragas/Bannerlord.MBOptionScreen) do it: when MCM is loaded it shows the block in its options screen and writes changes back into `SubModule.xml`. Updating UIExtenderEx replaces that file, so changed values need to be applied again afterwards.
* `CompiledPrefabs` (default `true`): set to `false` to skip the recompilation; affected movies then always load from XML.
* `DumpGeneratedCode` (default `false`): write the generated C# next to the cached assemblies.
* `DisableGeneratedPrefabs` (default `false`): the previous behaviour, every movie loads from XML.
* `DumpXML` (default `false`): dump the patched XML of every movie.
* `RecordTimings` (default `false`): write movie load, compile, and warm-up timings to `CompiledPrefabs/Timings/`, for investigating load times.

`CompiledPrefabs` and `DisableGeneratedPrefabs` are read at startup and need a restart. The two dump settings apply within a second of the file being saved.

For mod authors, [Compiled Prefabs](https://butr.github.io/Bannerlord.UIExtenderEx/articles/general/CompiledPrefabs.html) describes what changes for a mod and how to check that a movie is compiled. For maintainers, the [Compiled Prefabs](https://butr.github.io/Bannerlord.UIExtenderEx/articles/compiled-prefabs/Overview.html) section of the documentation describes the pipeline: the load decision, the manager and its cache, the fingerprint, the forked generator, compilation and the tests.
