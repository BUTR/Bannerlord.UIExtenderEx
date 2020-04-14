Library for Mount & Blade: Bannerlord that enables multiple mods to alter standard game interface.

### Installation
Install from NuGet: package name `UIExtenderLib`.

Alternatively you can download pacakge from [NuGet page](https://www.nuget.org/packages/UIExtenderLib/) and open it to find `dll` in the `lib/` folder.

### Updating from version 1.0.x
UIExtenderLib doesn't include `UIExtenderLibModule` now, meaning that all you need to do is add `dll` as a dependency. Mixing v1 modules and v2 modules are not supported, meaning that you absolutely need to update dependent mods. API stayed the same except for small changes in registration routine.

### When you should use it
If you change any of the game standard _prefab_ `.xml` files you should use this library or similar approach in order to not overwrite changes to the same elements by other mods.
You don't need to use this if you are adding a completely new screen or a menu item in the encounter overlay, since things like that are already handled correctly by the game API.

### Quickstart
You mark your _prefab extensions_ based on one of the `IPrefabPatch` descendants and marking it with `PrefabExtension` attribute, therefore enabling you to make additions to the specified Movie's XML data.

```cs
    [PrefabExtension("MapBar", "descendant::ListPanel[@Id='BottomInfoBar']/Children")]
    public class PrefabExtension : PrefabExtensionInsertPatch
    {
        public override int Position => PositionLast;
        public override string Name => "HorseAmountIndicator";
    }
```
This specific extension will load prefab `HorseAmountIndicator.xml` from `Mod/GUI/PrefabExtensions/` folder.

In order to add data to the prefab, you need to add properties to the target datasource class, which in case of `BottomInfoBar` is `MapInfoVM`, this is done by making a _mixin_ class, inheriting from `BaseViewModelMixin<T>` and marking it with `ViewModelMixin` attribute. This class will be mixed in to the target view model `T`, making fields and methods accessible in the prefab:

```cs
    [ViewModelMixin]
    public class ViewModelMixin : BaseViewModelMixin<MapInfoVM>
    {
        private int _horsesAmount;
        private string _horsesTooltip;
        
        [DataSourceProperty] public BasicTooltipViewModel HorsesAmountHint => new BasicTooltipViewModel(() => _horsesTooltip);
        [DataSourceProperty] public string HorsesAmount => "" + _horsesAmount;

        public ViewModelMixin(MapInfoVM vm) : base(vm)
        {
        }
        
        public override void OnRefresh()
        {
            var horses = MobileParty.MainParty.ItemRoster.Where(i => i.EquipmentElement.Item.ItemCategory.Id == new MBGUID(671088673));
            var newTooltip = horses.Aggregate("Horses: ", (s, element) => $"{s}\n{element.EquipmentElement.Item.Name}: {element.Amount}");

            if (newTooltip != _horsesTooltip)
            {
                _horsesAmount = horses.Sum(item => item.Amount);
                _horsesTooltip = newTooltip;

                if (_vm.TryGetTarget(out var vm))
                {
                    vm.OnPropertyChanged(nameof(HorsesAmount));
                }
            }
        }
    }
```

The last thing is to call `UIExtender.Register` and `UIExtender.Verify` to apply your extensions:
```cs

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            
            _extender = new UIExtender("ModuleName");
            _extender.Register();
        }

        protected override void OnBeforeInitialScreenSetAsRoot() {
            _extender.Verify();
        }
```

### Documentation
Rest of the documentation is located on [wiki](https://github.com/shdwp/UIExtenderLib/wiki).


### Examples
* [CampMod](https://github.com/shdwp/BannerlordCampMod) - mod that adds player camps
* [HorseAmountIndicator](https://github.com/shdwp/BannerlordHorseAmountIndicatorMod) - mod that adds horse amount indicator
