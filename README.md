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
  <a title="Crowdin" target="_blank" href="https://crowdin.com/project/uiextenderex">
    <img src="https://badges.crowdin.net/uiextenderex/localized.svg" alt="Crowdin">
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
Check the [``Articles``](https://butr.github.io/Bannerlord.UIExtenderEx/articles/v2/Overview.html) section of our documentation!

## Current State of AutoGens
The game uses two Prefab systems - static (pre-compiled XML) C# prefabs and dynamically serialized XML prefabs.  
The XML prefabs were introduced with the Early Access.  
The C# prefabs were introduced in the middle of Early Access. Most likely for Console releases, since they use the `Mono` runtime.  

We call AutoGens the XML prefabs that are pre-compiled into C# prefabs. The pre-compilation is achieved by using `TaleWorlds.MountAndBlade.GauntletUI.CodeGenerator.exe`.
It generates C# code based on the XML file. The C# code can then be compiled into an assembly (`.dll` file) that can be loaded by the game.  
This has the following benefits:
* We have ready-to-use prefabs at the very start of the game, removing the serialization step of the XML prefabs, which reduces the load time.
* We have static (typed) access to anything within the prefab. We do not need to use reflection to get/set data, which, again, speeds up the game. This is particularly noticeable on the `Mono` runtime.  

In summary, AutoGens are faster than the raw XML prefabs. The most performance is gained on the `Mono` runtime. On .NET (Core) the difference is more or less neglectable.

UIExtenderEx modifies the raw XML prefabs. Since the game does not use XML prefabs and instead relies on the AutoGens, our modifications will not affect the game.  
Currently, we just disable AutoGens globally. We are not able to do the pre-compilation at runtime. The issues are mostly not at our side - the `CodeGenerator` that the game provides doesn't support
such scenarios.
