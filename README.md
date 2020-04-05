Library for Mount & Blade: Bannerlord that enables multiple mods to alter standard game interface.

### Introduction
* `UIExtenderLib` is a library that you use in your project, which provides a number of attributes to decorate your classes with, which will
enable those classes to alter _Gauntlet_ prefabs and make additions to default `ViewModel`s.
* `UIExtenderLibModule` which should be loaded on target computer will then collect all of the extensions, and will patch them in on startup of the game.

### Installation
Download latest version of __UIExtenderLibModule__ from [releases](https://github.com/shdwp/UIExtenderLib/releases) and drop it into your `Modules` folder. `UIExtenderLib` doesn't need to be installed as it comes with the mods.

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
        
        public override void Refresh()
        {
            var horses = MobileParty.MainParty.ItemRoster.Where(i => i.EquipmentElement.Item.ItemCategory.Id == new MBGUID(671088673));
            var newTooltip = horses.Aggregate("Horses: ", (s, element) => $"{s}\n{element.EquipmentElement.Item.Name}: {element.Amount}");

            if (newTooltip != _horsesTooltip)
            {
                _horsesAmount = horses.Sum(item => item.Amount);
                _horsesTooltip = newTooltip;

                _vm.OnPropertyChanged(nameof(HorsesAmount));
            }
        }
    }
```

The last thing is to call `UIExtender.Register();` to register your attributed classes:
```cs

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            
            UIExtender.Register();
        }
```

### Documentation
Rest of the documentation is located on [wiki](https://github.com/shdwp/BannerlordCampMod/wiki).


### Examples
* [CampMod](https://github.com/shdwp/BannerlordCampMod) - mod that adds player camps
* [HorseAmountIndicator](https://github.com/shdwp/BannerlordHorseAmountIndicatorMod) - mod that adds horse amount indicator
