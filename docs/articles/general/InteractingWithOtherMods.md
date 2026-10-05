# Interacting with Other Mods

In a modded Bannerlord setup, multiple mods often extend the same Gauntlet UI screens. Occasionally, you may need to coordinate with or modify another mod's UI extensions—for instance, to resolve visual conflicts, build a compatibility bridge, or replace another mod's widget with your own customized implementation.

UIExtenderEx provides built-in discovery and control APIs that allow you to locate another mod's `UIExtender` instance and programmatically toggle or deregister its patches and mixins.

---

## Locating Another Mod's Extender

You can retrieve another mod's extender instance using `UIExtender.GetUIExtenderFor(string moduleName)`.

* **Module Name:** The string passed to this method must match the identifier the target mod used when calling `UIExtender.Create("TargetId")`.
* **Registration Requirement:** `GetUIExtenderFor` returns the `UIExtender` instance only after the target mod has called its `Register()` method. If the target mod has not been loaded or has not yet registered its assembly, the method returns `null`.

Always use null checks (or C# pattern matching) before interacting with the retrieved instance:

```csharp
if (UIExtender.GetUIExtenderFor("TargetModId") is { } targetExtender)
{
    // Interact with targetExtender
}
```

---

## Controlling Patches and Mixins

Once you have a reference to another mod's `UIExtender`, you can modify its behavior at two levels: the entire extender, or individual patch and mixin types.

### 1. Controlling Individual Patches or Mixins

Often, you only want to disable a single conflicting button or mixin property without turning off the rest of the other mod's features. You can enable or disable specific types using their `System.Type`:

```csharp
using Bannerlord.UIExtenderEx;
using HarmonyLib;

// Locate the target mod's extender (e.g., Mod Configuration Menu)
if (UIExtender.GetUIExtenderFor("MCM.UI") is { } mcmExtender)
{
    // Resolve the patch or mixin type (using AccessTools for soft dependencies)
    var conflictingPrefabPatch = AccessTools.TypeByName("MCM.UI.UIExtenderEx.OptionsPrefabExtension1");
    if (conflictingPrefabPatch is not null)
    {
        // Disable only this specific prefab patch
        mcmExtender.Disable(conflictingPrefabPatch);
    }

    var conflictingMixin = AccessTools.TypeByName("MCM.UI.UIExtenderEx.OptionsVMMixin");
    if (conflictingMixin is not null)
    {
        // Disable only this specific ViewModel mixin
        mcmExtender.Disable(conflictingMixin);
    }
}
```

If you later need to restore the extension, you can re-enable it:

```csharp
mcmExtender.Enable(conflictingPrefabPatch);
```

> [!TIP]
> If your mod has a direct assembly reference (hard dependency) to the target mod, you can pass `typeof(ConflictingPatchType)` directly instead of resolving it by name with `AccessTools.TypeByName`.

### 2. Controlling the Entire Extender

If you need to disable or enable all patches and mixins belonging to a mod at once:

```csharp
// Temporarily disable all patches and mixins registered by the mod
mcmExtender.Disable();

// Re-enable all patches and mixins registered by the mod
mcmExtender.Enable();
```

### 3. Permanently Deregistering an Extender

If you want to completely unload another mod's extensions with no possibility of re-enabling them during the session:

```csharp
// Unregisters the runtime and removes the extender completely
mcmExtender.Deregister();
```

> [!WARNING]
> Calling `Deregister()` completely removes the extender from the runtime lookup table and cleans up its internal resources. You cannot re-enable a deregistered extender with `Enable()`. Use `Disable()` instead if the extension might need to be re-activated later.

---

## Lifecycle and Execution Timing

When altering another mod's extensions, timing and load order are critical.

### SubModule Initialization Order

1. **Dependency Configuration:** Ensure your mod loads *after* the target mod. Declare the target mod in your `SubModule.xml` under `<DependedModules>` or configure the target mod under `<ModulesToLoadAfterThis>`.
2. **Execution Hook:**
   * Most mods initialize and register their extender inside `MBSubModuleBase.OnSubModuleLoad()`. In this standard case, you can place your override logic in your own `OnSubModuleLoad()`.
   * Some mods (such as MCM) defer registration to `OnBeforeInitialModuleScreenSetAsRoot()`. If the target mod registers there, execute your compatibility overrides in your own `OnBeforeInitialModuleScreenSetAsRoot()` method after the base call.

### Runtime Impact and Screen Refresh

* **Open Screens:** Gauntlet builds its widget hierarchy when a screen or movie is loaded. If a screen is already open, altering its patches will not modify the live widgets currently displayed. The changes take effect the next time that screen or movie opens.
* **Compiled Prefabs and Cache Invalidation:** When you call `Disable()`, `Enable()`, or `Deregister()`, UIExtenderEx invalidates the cached prefab templates and updates the active fingerprint for affected compiled movies. The next time the movie opens, Gauntlet will load the updated XML or recompile a new variant reflecting the active patches. See [Enabling, disabling and deregistering](CompiledPrefabs.md#enabling-disabling-and-deregistering) for technical details on cache invalidation.
