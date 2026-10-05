# ViewModel Mixins

In Gauntlet's Model-View-ViewModel (MVVM) architecture, Movie XML prefabs represent the **View** (declaring visual hierarchy and data bindings), while C# `ViewModel` classes manage state, business logic, and command callbacks.

Because Bannerlord's engine instantiates its own `ViewModel` classes directly, mods cannot simply replace vanilla ViewModels with custom subclasses. **ViewModel Mixins** solve this limitation: they attach dynamically to target `ViewModel` instances at runtime and expose custom properties and methods as if they were natively declared on the game's ViewModel.

Injected widgets from your [Prefab Extensions](PrefabExtensionInsertPatch.md) can then bind to these mixin members using standard Gauntlet binding syntax (`@PropertyName` and `@MethodName`).

---

## Anatomy of a ViewModel Mixin

A mixin is created by inheriting from [`BaseViewModelMixin<TViewModel>`](xref:Bannerlord.UIExtenderEx.ViewModels.BaseViewModelMixin`1) and decorating the class with the [`[ViewModelMixin]`](xref:Bannerlord.UIExtenderEx.Attributes.ViewModelMixinAttribute) attribute:

```csharp
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;

namespace MyMod.ViewModels;

[ViewModelMixin]
public sealed class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
{
    private bool _isCustomSettingEnabled;

    [DataSourceProperty]
    public bool IsCustomSettingEnabled
    {
        get => _isCustomSettingEnabled;
        set => SetField(ref _isCustomSettingEnabled, value);
    }

    [DataSourceProperty]
    public string CustomSettingLabel => "Enable Advanced Features";

    // Mixins must declare a public constructor accepting the target ViewModel instance
    public OptionsVMMixin(OptionsVM vm) : base(vm)
    {
        _isCustomSettingEnabled = true;
    }

    // Bound in prefab XML via Command.Click="@ExecuteResetSetting"
    [DataSourceMethod]
    public void ExecuteResetSetting()
    {
        IsCustomSettingEnabled = false;
    }
}
```

### Constructor and Lifecycle

* **Constructor Requirement:** Every mixin must have a `public` constructor that accepts `TViewModel` as its sole argument and passes it to `base(vm)`.
* **Automatic Instantiation:** UIExtenderEx instantiates a new mixin instance for every runtime instance of `TViewModel` at the end of the ViewModel's constructor.
* **Weak References:** The mixin references its target ViewModel via a weak reference (`WeakReference<TViewModel>`). When the game engine disposes or unloads the screen, the ViewModel is safely garbage-collected without memory leaks.

---

## Exposing Properties and Methods

Gauntlet inspects data sources through reflection. To make your mixin members visible to Gauntlet bindings:

* **Properties (`[DataSourceProperty]`):** Public properties decorated with `[DataSourceProperty]` are accessible in prefab XML using `@PropertyName` or `{PropertyName}`.
* **Methods (`[DataSourceMethod]`):** Public methods decorated with [`[DataSourceMethod]`](xref:Bannerlord.UIExtenderEx.Attributes.DataSourceMethodAttribute) can be bound to widget callbacks (such as `Command.Click="@MethodName"`).
* **Non-Public Members:** Any private, protected, or unannotated members remain internal to your mixin and are ignored by the Gauntlet binding engine.

> [!WARNING]
> **Method Name Collisions:** Avoid giving mixin methods the same name as existing methods on the game's ViewModel. A standard mixin method only replaces the binding target for prefab widgets, not internal C# callers, and the [Roslyn Analyzers](../general/Analyzers.md) will report a `UIX0001` diagnostic.
> 
> If you intentionally want to intercept or replace a vanilla ViewModel method across all game callers, use `[BUTRViewModelOverride]` instead. See [Mixin Hooks](../v3/Mixins.md#taking-over-a-viewmodel-method).

---

## Working with the Target ViewModel

[`BaseViewModelMixin<TViewModel>`](xref:Bannerlord.UIExtenderEx.ViewModels.BaseViewModelMixin`1) provides built-in helper properties and methods:

### 1. Accessing the Target Instance (`ViewModel`)

Use the protected `ViewModel` property to interact with the underlying game object:

```csharp
[DataSourceMethod]
public void ExecutePrintHeroName()
{
    if (ViewModel is { } vm)
    {
        // Access game data or native methods on the target ViewModel
    }
}
```

> [!NOTE]
> Always use null-checks or pattern matching when accessing `ViewModel`. If the game engine has finalized and garbage-collected the screen, `ViewModel` will return `null`.

### 2. Raising Property Notifications

When a mixin property changes, you must notify Gauntlet so bound widgets refresh their display:

* **`OnPropertyChanged([CallerMemberName] string? propertyName = null)`:** Triggers a standard notification using caller member attribution.
* **`OnPropertyChangedWithValue(object value, [CallerMemberName] string? propertyName = null)`:** Dispatches a typed change notification (supporting primitives, `Color`, `Vec2`, etc.).
* **`SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)`:** Updates the backing field and automatically raises `OnPropertyChanged` only if the new value differs from the existing value.

### 3. Listening to ViewModel Property Changes (`OnViewModelPropertyChanged`)

If your mixin properties depend on native ViewModel properties, you can listen to all notifications raised by the game ViewModel by overriding `OnViewModelPropertyChanged`:

```csharp
protected override void OnViewModelPropertyChanged(string propertyName)
{
    base.OnViewModelPropertyChanged(propertyName);

    if (propertyName == "CurrentSelectedCharacter")
    {
        // React to character change in the native screen
        OnPropertyChanged(nameof(CustomCharacterStats));
    }
}
```

UIExtenderEx automatically subscribes your mixin to all nine native property change events when overridden, and unsubscribes when the ViewModel is finalized.

### 4. Accessing Private Members

To read or modify private fields or properties on the game's ViewModel:
* `GetPrivate<T>(string memberName)`: Reads a private field or property using reflection.
* `SetPrivate<T>(string memberName, T value)`: Sets a private field or property.

> [!TIP]
> In UIExtenderEx 3.0, prefer `[BUTRUnsafeAccessor]` over reflection. It generates zero-overhead IL accessors that are validated at registration time. See [Reaching Private Members](../v3/Mixins.md#reaching-private-members).

### 5. Cleanup (`OnFinalize`)

Override `OnFinalize()` to release unmanaged resources, detach custom events, or unsubscribe from campaign behaviors when the ViewModel is destroyed:

> [!NOTE]
> A mixin's `OnFinalize()` method executes only when its target ViewModel's `OnFinalize()` is called by the game. However, native TaleWorlds ViewModels do not consistently finalize their child ViewModels: for example, `KingdomManagementVM.OnFinalize` explicitly finalizes `Decision` and `Clan`, but omits `Diplomacy`, `Army`, `Policy`, `Settlement`, and `GiftFief`. Mixins attached to unfinalized child ViewModels that register external event listeners or campaign behaviors must be finalized through an alternative mechanism (for example, Diplomacy manually invokes `ViewModel.Diplomacy.OnFinalize()` from its `KingdomManagementVM` mixin).

```csharp
public override void OnFinalize()
{
    base.OnFinalize();
    // Perform cleanup here
}
```

---

## Refresh Handling (`OnRefresh`)

Most Gauntlet ViewModels feature a refresh method (such as `Refresh()`, `RefreshValues()`, or `Update()`) that rebuilds data when the screen updates.

You can configure UIExtenderEx to call your mixin's `OnRefresh()` method whenever the native refresh method executes by specifying its name in the attribute:

```csharp
[ViewModelMixin("Refresh")] // Or [ViewModelMixin(nameof(MapInfoVM.Refresh))] if public
public sealed class MapInfoMixin : BaseViewModelMixin<MapInfoVM>
{
    public MapInfoMixin(MapInfoVM vm) : base(vm) { }

    public override void OnRefresh()
    {
        base.OnRefresh();
        // Update your mixin data after the vanilla screen refreshes
    }
}
```

> [!IMPORTANT]
> **The Refresh Rule:** Do not call `OnRefresh()` or the ViewModel's native refresh method from inside your mixin's constructor! Most vanilla ViewModels invoke their refresh method during their own constructor, which UIExtenderEx automatically forwards to your mixin as soon as construction finishes. See [The Refresh Rule](../v3/Mixins.md#the-refresh-rule).

---

## Extending Subclasses and Abstract ViewModels (`handleDerived`)

By default, a mixin attaches only to instances whose exact runtime type matches `TViewModel`.

If you want a mixin to attach to all types derived from `TViewModel`, set `handleDerived: true`:

```csharp
[ViewModelMixin(handleDerived: true)]
public class ItemVMMixin : BaseViewModelMixin<ItemVM>
{
    public ItemVMMixin(ItemVM vm) : base(vm) { }
}
```

> [!NOTE]
> Setting `handleDerived: true` is **mandatory** if `TViewModel` is an abstract class, since abstract types cannot be instantiated directly.

---

## SubModule Registration

Register your mixins alongside your prefab patches during mod startup in your `MBSubModuleBase`. For more details on unique IDs and cross-mod lifecycle coordination, see [Getting Started: Registering Your Mod](../general/Overview.md#getting-started-registering-your-mod):

```csharp
using Bannerlord.UIExtenderEx;
using TaleWorlds.MountAndBlade;

public class SubModule : MBSubModuleBase
{
    private static readonly UIExtender Extender = UIExtender.Create("MyModId");

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        // Scans the assembly and registers all [ViewModelMixin] and [PrefabExtension] classes
        Extender.Register(typeof(SubModule).Assembly);
        Extender.Enable();
    }
}
```

---

## Compile-Time Checking with Prefab Links

Tie your mixins to the prefab patches that bind them using [`[assembly: PrefabLink]`](PrefabLink.md). This enables the [Roslyn Analyzers](../general/Analyzers.md) to verify at build time that all property names, method signatures, and data bindings match between your C# code and Gauntlet XML.

