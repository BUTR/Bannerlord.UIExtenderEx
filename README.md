# Bannerlord.UIExtenderEx
<p align="center">
  <!--
  <a href="https://github.com/BUTR/Bannerlord.UIExtenderEx" alt="Logo">
  <img src="https://github.com/BUTR/Bannerlord.UIExtenderEx/blob/dev/resources/Butter.png?raw=true" /></a>
  </br>
  -->
  <a href="https://github.com/BUTR/Bannerlord.UIExtenderEx" alt="Lines Of Code">
  <img src="https://tokei.rs/b1/github/BUTR/Bannerlord.UIExtenderEx?category=code" /></a>
  <a href="https://www.codefactor.io/repository/github/butr/bannerlord.uiextenderex"><img src="https://www.codefactor.io/repository/github/butr/bannerlord.uiextenderex/badge" alt="CodeFactor" /></a>
  </br>
  <a href="https://github.com/BUTR/Bannerlord.UIExtenderEx/actions?query=workflow%3ATest"><img src="https://github.com/BUTR/Bannerlord.UIExtenderEx/workflows/Test/badge.svg?branch=dev&event=push" alt="Test" /></a>
  <a href="https://codecov.io/gh/BUTR/Bannerlord.UIExtenderEx"><img src="https://codecov.io/gh/BUTR/Bannerlord.UIExtenderEx/branch/dev/graph/badge.svg" />
   </a>
  </br>
  <a href="https://www.nuget.org/packages/Bannerlord.UIExtenderEx" alt="NuGet Bannerlord.UIExtenderEx">
  <img src="https://img.shields.io/nuget/v/Bannerlord.UIExtenderEx.svg?label=NuGet%20Bannerlord.UIExtenderEx&colorB=blue" /></a>
  <a href="https://butr.github.io/Bannerlord.UIExtenderEx" alt="Documentation">
  <img src="https://img.shields.io/badge/Documentation-%F0%9F%94%8D-blue?style=flat" /></a>
  </br>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="Nexus UIExtenderEx">
  <img src="https://img.shields.io/badge/Nexus-UIExtenderEx-yellow.svg" /></a>  
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="UIExtenderEx">
  <img src="https://img.shields.io/endpoint?url=https%3A%2F%2Fnexusmods-version-pzk4e0ejol6j.runkit.sh%3FgameId%3Dmountandblade2bannerlord%26modId%3D2102" /></a>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="Nexus UIExtenderEx">
  <img src="https://img.shields.io/endpoint?url=https%3A%2F%2Fnexusmods-downloads-ayuqql60xfxb.runkit.sh%2F%3Ftype%3Dunique%26gameId%3D3174%26modId%3D2102" /></a>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="Nexus UIExtenderEx">
  <img src="https://img.shields.io/endpoint?url=https%3A%2F%2Fnexusmods-downloads-ayuqql60xfxb.runkit.sh%2F%3Ftype%3Dtotal%26gameId%3D3174%26modId%3D2102" /></a>
  </br>
</p>

A library that enables multiple mods to alter standard game interface.

## Installation
This module should be one of the highest in loading order. Ideally, it should be loaded after ``Bannerlord.Harmony`` or ``Bannerlord.ButterLub``.

## For Players
This mod is a dependency mod that does not provide anything by itself. You need to additionaly install mods that use it.
  
  
  
## Quickstart
You mark your _prefab extensions_ based on one of the `IPrefabPatch` descendants and marking it with `PrefabExtension` attribute, therefore enabling you to make additions to the specified Movie's XML data.

Example of inserting ``XML`` at a specific position:
```csharp
    [PrefabExtension("Insert", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children")]
    internal class TestPrefabExtensionInsertPatch : PrefabExtensionInsertPatch
    {
        public override string Id => "Insert";
        public override int Position => 3;
        private XmlDocument XmlDocument { get; } = new XmlDocument();

        public TestPrefabExtensionInsertPatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"Insert\" />");
        }

        public override XmlDocument GetPrefabExtension() => XmlDocument;
    }
```

Example of replacing ``XML``:
```csharp
    [PrefabExtension("Replace", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='Replace']")]
    internal class TestPrefabExtensionReplacePatch : PrefabExtensionReplacePatch
    {
        public override string Id => "Replace";
        private XmlDocument XmlDocument { get; } = new XmlDocument();

        public TestPrefabExtensionReplacePatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"Replaced\" />");
        }

        public override XmlDocument GetPrefabExtension() => XmlDocument;
    }
```

Example of inserting ``XML`` after a specific element:
```csharp
    [PrefabExtension("InsertAsSiblingAppend", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='InsertAsSibling']")]
    internal class TestPrefabExtensionInsertAsSiblingAppendPatch : PrefabExtensionInsertAsSiblingPatch
    {
        public override string Id => "InsertAsSiblingAppend";
        public override InsertType Type => InsertType.Append;
        private XmlDocument XmlDocument { get; } = new XmlDocument();

        public TestPrefabExtensionInsertAsSiblingAppendPatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"InsertAsSiblingAppend\" />");
        }

        public override XmlDocument GetPrefabExtension() => XmlDocument;
    }
```

Example of inserting ``XML`` before a specific element:
```csharp
    [PrefabExtension("InsertAsSiblingPrepend", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='InsertAsSibling']")]
    internal class TestPrefabExtensionInsertAsSiblingPrependPatch : PrefabExtensionInsertAsSiblingPatch
    {
        public override string Id => "InsertAsSiblingPrepend";
        public override InsertType Type => InsertType.Prepend;
        private XmlDocument XmlDocument { get; } = new XmlDocument();

        public TestPrefabExtensionInsertAsSiblingPrependPatch()
        {
            XmlDocument.LoadXml("<OptionsTabToggle Id=\"InsertAsSiblingPrepend\" />");
        }

        public override XmlDocument GetPrefabExtension() => XmlDocument;
    }
```
In order to add data to the prefab, you need to add properties to the target datasource class, this is done by making a _mixin_ class, inheriting from `BaseViewModelMixin<T>` and marking it with `ViewModelMixin` attribute. This class will be mixed in to the target view model `T`, making fields and methods accessible in the prefab:

```csharp
    [ViewModelMixin]
    public class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
    {
        private readonly ModOptionsVM _modOptions;

        [DataSourceProperty]
        public ModOptionsVM ModOptions
        {
            get
            {
                return _modOptions;
            }
        }

        public OptionsVMMixin(OptionsVM vm) : base(vm)
        {
            _modOptions = new ModOptionsVM();
        }

        [DataSourceMethod]
        public void ExecuteCloseOptions()
        {
            ModOptions.ExecuteCancelInternal(false);
            ViewModel?.ExecuteCloseOptions();
        }
    }
```

The last thing is to call `UIExtender.Register` and `UIExtender.Enable` to apply your extensions:
```cs
      public class CustomSubModule : MBSubModuleBase
      {
          protected override void OnSubModuleLoad()
          {
              base.OnSubModuleLoad();
            
              _extender = new UIExtender("ModuleName");
              _extender.Register(typeof(CustomSubModule).Assembly);
              _extender.Enable();
          }
        }
```

To use the `OnRefresh` overload you will need to specify for UIExtenderEx the underlying method that acts as the conceptual 'Refresh' method in the `ViewModel`.  
For example, `MapInfoVM` has a method `Refresh`.  
If such method exists, specify it in the `ViewModelMixin` like this:
```csharp
[ViewModelMixin("Refresh")] // or [ViewModelMixin(nameof(MapInfoVM.Refresh))] // if the method is public
public class MapInfoMixin : BaseViewModelMixin<MapInfoVM>
```

### Examples
* [Bannerlord.MBOptionScreen](https://github.com/Aragas/Bannerlord.MBOptionScreen/tree/dev/src/MCM.UI/UIExtenderEx)
* [Yet Another Party Organiser](https://github.com/tbeswick96/BannerlordYetAnotherPartyOrganiser)
