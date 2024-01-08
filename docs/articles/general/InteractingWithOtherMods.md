# Interacting with Other Mods

You can access another mod's `UIExtender` and modify it to your liking.  
At the moment you are able to disable the UIExtender, deregister it (meaning fully disabling it without the ability to enable it back) and enable.  
You are able to disable a specific Prefab or Mixin.  
```csharp
// Get Mod Configuration Menu's UIExtender
var mcm = UIExtender.GetUIExtenderFor("MCM.UI");

// Disable a prefab
var mcmPrefab = AccessTools.TypeByName("MCM.UI.UIExtenderEx.OptionsPrefabExtension1");
mcm.Disable(mcmPrefab);

// Disable a Mixin
var mcmMixin = AccessTools.TypeByName("MCM.UI.UIExtenderEx.OptionsVMMixin");
mcm.Disable(mcmMixin);
```