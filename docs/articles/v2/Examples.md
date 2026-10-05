# Worked Examples & Real-World Projects

Examining open-source production mods is one of the best ways to learn advanced Gauntlet UI patterns and see UIExtenderEx in practice.

---

## Featured Open-Source Mods

### 1. Mod Configuration Menu (MCM)
* **GitHub Repository:** [MCM on GitHub](https://github.com/Aragas/Bannerlord.MBOptionScreen/tree/dev/src-ui/MCM.UI/UIExtenderEx)
* **Case Study:** [MCM In-Depth Case Study](../v3/MCM.md)

**Key Techniques Demonstrated:**
* Injects a dedicated mod settings tab into Bannerlord's native `Options` screen.
* Uses `InsertType.Prepend` to add its tab toggle and tab panel adjacent to the vanilla tabs.
* Dynamically registers custom widget C# classes and standalone XML prefabs at runtime using `WidgetFactoryManager`.
* Implements robust ViewModel mixins to handle responsive settings updates, search filtering, and user inputs.

---

### 2. Diplomacy
* **Case Study:** [Diplomacy In-Depth Case Study](../v3/Diplomacy.md)

**Key Techniques Demonstrated:**
* Extends the native Kingdom management screen (`KingdomManagementVM`) with custom diplomacy tabs, alliance pacts, and war exhaustion meters.
* Adds more than 70 properties and commands across seven vanilla ViewModels using ten mixins, without needing to override game methods (`[BUTRViewModelOverride]`) or access private fields (`[BUTRUnsafeAccessor]`).
* Hooks a named refresh method (`UpdateDiplomacyProperties`) instead of `RefreshValues`.
* Subscribes to native property notifications via `OnViewModelPropertyChanged` to keep modded widgets in sync with campaign state.

---

### 3. Settlement Icons
* **GitHub Repository:** [Settlement Icons on GitHub](https://github.com/BUTR/Bannerlord.SettlementIcons/tree/master/src/SettlementIcons/UIExtenderEx)

**Key Techniques Demonstrated:**
* Lightweight campaign map UI extensions.
* Adds custom overlay icons and information markers directly to settlement nameplates on the world map.
* Demonstrates clean lifecycle management, weak reference handling, and disposal via `OnFinalize`.

---

## Complete End-to-End Starter Example

Below is a self-contained blueprint demonstrating how all the pieces connect in a real mod:

### 1. SubModule Registration

```csharp
// SubModule.cs
using Bannerlord.UIExtenderEx;
using TaleWorlds.MountAndBlade;

namespace MyCustomMod;

public class SubModule : MBSubModuleBase
{
    private static readonly UIExtender Extender = UIExtender.Create("MyCustomMod");

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        // Register all mixins and prefab patches defined in this assembly
        Extender.Register(typeof(SubModule).Assembly);
        Extender.Enable();
    }
}
```

### 2. ViewModel Mixin

```csharp
// OptionsVMMixin.cs
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

namespace MyCustomMod;

[ViewModelMixin]
internal sealed class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
{
    private bool _myFeatureEnabled = true;

    [DataSourceProperty]
    public bool MyFeatureEnabled
    {
        get => _myFeatureEnabled;
        set => SetField(ref _myFeatureEnabled, value);
    }

    [DataSourceProperty]
    public string MyFeatureButtonText => "Toggle Custom Feature";

    public OptionsVMMixin(OptionsVM vm) : base(vm) { }

    [DataSourceMethod]
    public void ExecuteToggleFeature()
    {
        MyFeatureEnabled = !MyFeatureEnabled;
    }
}
```

### 3. Prefab Patch

```csharp
// OptionsButtonPatch.cs
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace MyCustomMod;

[PrefabExtension("Options", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children")]
internal sealed class OptionsButtonPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;
    public override int Index => 2;

    [PrefabExtensionText]
    public string Content => """
        <ButtonWidget DoNotPassEventsToChildren="true"
                      WidthSizePolicy="CoverChildren"
                      HeightSizePolicy="CoverChildren"
                      Command.Click="@ExecuteToggleFeature">
            <Children>
                <TextWidget WidthSizePolicy="CoverChildren"
                            HeightSizePolicy="CoverChildren"
                            Text="@MyFeatureButtonText" />
            </Children>
        </ButtonWidget>
        """;
}
```

### 4. Compile-Time Linking

```csharp
// UILinks.cs
using Bannerlord.UIExtenderEx.Attributes;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

[assembly: PrefabLink(typeof(MyCustomMod.OptionsButtonPatch), typeof(OptionsVM), typeof(MyCustomMod.OptionsVMMixin))]
```

