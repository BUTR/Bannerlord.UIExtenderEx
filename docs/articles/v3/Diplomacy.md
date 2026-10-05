# Worked Example: Diplomacy

The [Diplomacy](https://github.com/DiplomacyTeam/Bannerlord.Diplomacy) mod introduces extensive diplomatic features to *Mount & Blade II: Bannerlord*, including alliances, non-aggression pacts, war exhaustion mechanics, messenger systems, and kingdom voting overhauls.

Architecturally, Diplomacy represents a pure **additive UI extension**: ten mixins contribute more than 70 custom properties and commands to seven vanilla ViewModels, bound via Diplomacy's custom prefab templates. 

Because Diplomacy does not need to replace vanilla buttons or read private game fields, it requires neither `[BUTRViewModelOverride]` nor `[BUTRUnsafeAccessor]`. Instead, its adoption of UIExtenderEx 3.0 demonstrates **code simplification and deletion**: eliminating redundant refresh calls and replacing manual event subscriptions with native notification hooks. Introducing an intermediate generic base class is an additional architectural option for sharing common properties across related mixins (such as war and truce items).

---

## 1. Eliminating Redundant Refreshes

In UIExtenderEx 2.x, five Diplomacy mixins manually invoked `OnRefresh()` or `vm.RefreshValues()` directly within their constructors.

As detailed in [The refresh rule](Mixins.md#the-refresh-rule), TaleWorlds ViewModels almost universally call their refresh logic within their own constructors. UIExtenderEx intercepts that initial refresh and automatically forwards it to your mixin's `OnRefresh()` method **immediately after your mixin finishes constructing**.

Calling `OnRefresh()` inside the constructor caused every mixin to execute its data initialization twice on every screen load.

### The Fix: Deleting Manual Constructor Calls

```diff
 public KingdomWarItemVMMixin(KingdomWarItemVM vm) : base(vm)
 {
     PactsText = _TNonAggressionPacts.ToString();
-    OnRefresh();
+    // No OnRefresh() call needed: KingdomWarItemVM's constructor calls RefreshValues, which calls
+    // UpdateDiplomacyProperties, this mixin's refresh method. UIExtenderEx forwards that call to
+    // OnRefresh() once the mixin is constructed.
 }
```

### Removing Defensive Constructor Guards

Because developers previously feared `OnRefresh()` might execute before the mixin constructor, mixins often contained defensive null-checks. In UIExtenderEx 3.0, `OnRefresh()` is guaranteed never to run before the constructor finishes, allowing you to safely remove defensive guards:

```diff
 public override void OnRefresh()
 {
-    // Guard no longer needed: OnRefresh is guaranteed to run after construction
-    if (_hero is null)
-    {
-        return;
-    }
-
     if (_hero.Clan?.Kingdom is not null && ...)
     {
         // Refresh display values
     }
 }
```

---

## 2. Universal Property Notifications (`OnViewModelPropertyChanged`)

Diplomacy's `KingdomClanVMMixin` enables or disables the "Grant Fief" and "Donate Gold" buttons whenever the user selects a different clan in the Kingdom management interface (`KingdomClanVM.CurrentSelectedClan`).

### In 2.x: Manual Event Subscription Boilerplate

In 2.x, developers had to manually instantiate a delegate, subscribe to the ViewModel's event in the constructor, and remember to unsubscribe inside `OnFinalize`:

```csharp
// 2.x Approach: Manual event wiring and teardown
private readonly PropertyChangedWithValueEventHandler _eventHandler;

public KingdomClanVMMixin(KingdomClanVM vm) : base(vm)
{
    _eventHandler = new PropertyChangedWithValueEventHandler(OnPropertyChangedWithValue);
    ViewModel!.PropertyChangedWithValue += _eventHandler;
}

public override void OnFinalize()
{
    DiplomacyEvents.RemoveListeners(this);
    ViewModel!.PropertyChangedWithValue -= _eventHandler;
    base.OnFinalize();
}

private void OnPropertyChangedWithValue(object sender, PropertyChangedWithValueEventArgs e)
{
    if (e.PropertyName == nameof(KingdomClanVM.CurrentSelectedClan))
    {
        RefreshCanGrantFief();
    }
}
```

### In 3.0: Declarative Hook

In 3.0, you simply override `OnViewModelPropertyChanged`. UIExtenderEx automatically manages subscribing to all nine typed notification events and cleanly detaches them on finalization:

```csharp
// 3.0 Approach: Clean, boilerplate-free override
protected override void OnViewModelPropertyChanged(string propertyName)
{
    base.OnViewModelPropertyChanged(propertyName);

    if (propertyName == nameof(KingdomClanVM.CurrentSelectedClan))
    {
        RefreshCanGrantFief();
    }
}
```

---

## 3. Organizing Shared Logic with Generic Mixin Base Classes

Diplomacy's war items (`KingdomWarItemVM`) and truce items (`KingdomTruceItemVM`) share identical UI components (such as war exhaustion meters and tribute indicators).

Currently, both mixins declare these properties independently, deriving directly from `BaseViewModelMixin<T>`. Introducing an intermediate generic base class illustrates how to share common property definitions and calculations across related screens:

```csharp
// Shared base class containing common properties and logic
internal abstract class DiplomacyItemMixin<TViewModel> : BaseViewModelMixin<TViewModel> 
    where TViewModel : ViewModel
{
    [DataSourceProperty]
    public string WarExhaustionText { get; set; } = string.Empty;

    [DataSourceProperty]
    public float WarExhaustionValue { get; set; }

    protected DiplomacyItemMixin(TViewModel vm) : base(vm) { }

    protected void UpdateExhaustion(float value)
    {
        WarExhaustionValue = value;
        WarExhaustionText = $"{value:P0}";
    }
}

// Derived mixin for War items
[ViewModelMixin("UpdateDiplomacyProperties")]
internal sealed class KingdomWarItemVMMixin : DiplomacyItemMixin<KingdomWarItemVM>
{
    public KingdomWarItemVMMixin(KingdomWarItemVM vm) : base(vm) { }

    public override void OnRefresh()
    {
        base.OnRefresh();
        UpdateExhaustion(CalculateWarExhaustion());
    }
}

// Derived mixin for Truce items
[ViewModelMixin("UpdateDiplomacyProperties")]
internal sealed class KingdomTruceItemVMMixin : DiplomacyItemMixin<KingdomTruceItemVM>
{
    public KingdomTruceItemVMMixin(KingdomTruceItemVM vm) : base(vm) { }

    public override void OnRefresh()
    {
        base.OnRefresh();
        UpdateExhaustion(CalculateTruceExhaustion());
    }
}
```

UIExtenderEx inspects the full type hierarchy, automatically discovering the target `ViewModel` type from `BaseViewModelMixin<T>` and registering all public `[DataSourceProperty]` members declared on both the base and derived classes.

---

## 4. Multi-Mod Conflict Detection

In the broader Bannerlord modding ecosystem, multiple mods may attempt to extend the same screen. For example, both Diplomacy and other campaign mods (such as *TAOM* or *A World of Ice and Fire*) introduce messenger capabilities to `EncyclopediaHeroPageVM` using identical property names:
* `IsMessengerAvailable`
* `SendMessengerActionName`
* `SendMessengerHint`

In UIExtenderEx 2.x, whoever registered last silently overwrote the property in the data binding table with no notification.

In UIExtenderEx 3.0, `Extender.Register(...)` explicitly detects duplicate member registrations across mods, logs an informative warning identifying both mods, and notes which property was bound. For more details on diagnostic behavior, see [What registration reports](Mixins.md#what-registration-reports).
