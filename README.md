Library for Mount & Blade: Bannerlord that enables multiple mods to alter standard game interface.

For internal usage only for now.

### Quickstart
You mark your _prefab extensions_ based on one of the `IPrefabPatch` descendants and marking it with `PrefabExtension` attribute, therefore enabling you to make additions to the specified Movie's XML data.

```cs
    [PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children")]
    public class OptionsPrefabExtension1 : PrefabExtensionInsertPatch
    {
        public override string Id => "Options1";
        public override int Position => 3;
        private XmlDocument XmlDocument { get; } = new XmlDocument();

        public OptionsPrefabExtension1()
        {
            XmlDocument.LoadXml("<OptionsTabToggle DataSource=\"{ModOptions}\" PositionYOffset=\"2\" Parameter.ButtonBrush=\"Header.Tab.Center\" Parameter.TabName=\"ModOptionsPage\" />");
        }

        public override XmlDocument GetPrefabExtension() => XmlDocument;
    }
```
This specific extension will load prefab `HorseAmountIndicator.xml` from `MODULE/GUI/PrefabExtensions/` folder.

In order to add data to the prefab, you need to add properties to the target datasource class, which in case of `BottomInfoBar` is `MapInfoVM`, this is done by making a _mixin_ class, inheriting from `BaseViewModelMixin<T>` and marking it with `ViewModelMixin` attribute. This class will be mixed in to the target view model `T`, making fields and methods accessible in the prefab:

```cs
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
* [Bannerlord.MBOptionScreen](https://github.com/Aragas/Bannerlord.MBOptionScreen/tree/v3/MCM.UI/UIExtenderEx)
