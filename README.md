A library that enables multiple mods to alter standard game interface.

### Quickstart
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

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            
            _extender = new UIExtender("ModuleName");
            _extender.Register();
            _extender.Enable();
        }
```

### Examples
* [Bannerlord.MBOptionScreen](https://github.com/Aragas/Bannerlord.MBOptionScreen/tree/v3/src/MCM.UI/UIExtenderEx)
