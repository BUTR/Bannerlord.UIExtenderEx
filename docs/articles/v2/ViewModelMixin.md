### ViewModelMixin

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