# v1 Documentation
If possible, it is recommended to now use the PrefabsV2 API.

## Quickstart
You mark your _prefab extensions_ based on one of the [``IPrefabPatch``](xref:Bannerlord.UIExtenderEx.Prefabs.IPrefabPatch) descendants and marking it with [``PrefabExtension``](xref:Bannerlord.UIExtenderEx.Attributes.PrefabExtensionAttribute) attribute, therefore enabling you to make additions to the specified Movie's XML data.

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

Example of adding or replacing  ``XML`` attribute:
```csharp
    [PrefabExtension("SetAttribute", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='SetAttribute']")]
    internal class TestPrefabExtensionSetAttributePatch : PrefabExtensionSetAttributePatch
    {
        public override string Id => "SetAttribute";
        public override string Attribute => "CustomAttribute";
        public override string Value => "Value";
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
In order to add data to the prefab, you need to add properties to the target datasource class, this is done by making a _mixin_ class, inheriting from [``BaseViewModelMixin<T>``](xref:Bannerlord.UIExtenderEx.ViewModels.BaseViewModelMixin`1) and marking it with [``ViewModelMixin``](xref:Bannerlord.UIExtenderEx.Attributes.ViewModelMixinAttribute) attribute. This class will be mixed in to the target view model `T`, making fields and methods accessible in the prefab:

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

To use the `OnRefresh` overload you will need to specify for UIExtenderEx the underlying method that acts as the conceptual 'Refresh' method in the [``ViewModel``](xref:TaleWorlds.Library.ViewModel).  
For example, [``MapInfoVM``](xref:TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapInfoVM) has a method `Refresh`.  
If such method exists, specify it in the [``ViewModelMixin``](xref:Bannerlord.UIExtenderEx.Attributes.ViewModelMixinAttribute) like this:
```csharp
[ViewModelMixin("Refresh")] // or [ViewModelMixin(nameof(MapInfoVM.Refresh))] // if the method is public
public class MapInfoMixin : BaseViewModelMixin<MapInfoVM>
```

### Examples
* [Bannerlord.MBOptionScreen](https://github.com/Aragas/Bannerlord.MBOptionScreen/tree/dev/src/MCM.UI/UIExtenderEx)
* [Yet Another Party Organiser](https://github.com/tbeswick96/BannerlordYetAnotherPartyOrganiser)
