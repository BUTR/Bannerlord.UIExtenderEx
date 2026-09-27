# Prefab Links

Extending a game screen usually takes two pieces: a [ViewModel Mixin](ViewModelMixin.md) that adds members to the game's
ViewModel, and a [prefab patch](PrefabExtensionInsertPatch.md) that inserts the XML binding them. Nothing in either says
they belong together. `[assembly: PrefabLink]` does: it names the patch, the ViewModel its XML binds, and the mixin whose
members it binds.

```csharp
using Bannerlord.UIExtenderEx.Attributes;

[assembly: PrefabLink(typeof(ModOptionsPagePatch), typeof(OptionsVM), typeof(OptionsVMMixin))]
[assembly: PrefabLink("ModOptionsView_MCM", typeof(ModOptionsVM))]

[ViewModelMixin]
internal sealed class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
{
    public OptionsVMMixin(OptionsVM vm) : base(vm) { }

    [DataSourceProperty] public ModOptionsVM ModOptions { get; } = new();
    [DataSourceProperty] public int DescriptionWidth { get; set; }
}

[PrefabExtension("Options", "descendant::Widget[@Id='OptionsPanel']")]
internal sealed class ModOptionsPagePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    [PrefabExtensionText]
    public string GetContent() => "<ModOptionsView_MCM DataSource=\"{ModOptions}\" />";
}
```

The attribute comes with [Bannerlord.UIExtenderEx.Analyzers](../general/Analyzers.md): with the package referenced,
`[assembly: PrefabLink]` compiles against any UIExtenderEx 2.x. The package adds it to your project as an internal type
when your UIExtenderEx does not have one, and leaves no trace of your links in the built assembly. UIExtenderEx 3.0
declares it itself; your links compile unchanged against either.

Assembly attributes can go in any file of the project. Keeping all of a mod's links in one file, such as `UILinks.cs`,
gives a single list of every screen the mod changes and what drives each change.

## What it links

| Argument | What it is |
| --- | --- |
| First | The prefab patch: a class marked `[PrefabExtension]` deriving from `PrefabExtensionInsertPatch` or `PrefabExtensionSetAttributePatch`, or from one of the v1 patches in `Bannerlord.UIExtenderEx.Prefabs`. Or, as a string, a prefab of your own: the name you register it under with `WidgetFactoryManager.CreateAndRegister`, or its file name when you do not register it. |
| Second | The ViewModel the XML binds where it goes in. For a patch, the one at the node the XPath selects. For your own prefab, the one at its root. |
| Third (optional) | The mixin whose members the XML binds. It has to be attached to that ViewModel: it extends it, or it extends a base of it and has `handleDerived: true`. |

A patch can have several links, one per mixin it binds, as long as they all name the same ViewModel. The XML binds one
ViewModel where it goes in.

## What reads it

[Bannerlord.UIExtenderEx.Analyzers](../general/Analyzers.md) reads it while you build. UIExtenderEx does not read it at
runtime: a link changes nothing about how your UI is registered or applied.

Without a link, the analyzer works out a patch's ViewModel from your mixins: the ViewModel one of them extends that has
the names the patch binds, with at least one name added by a mixin. With a link, it takes the ViewModel you name, and:

* checks every binding and command in the patch, and in the prefabs it reaches, against that ViewModel and your mixins
  on it (UIX0015)
* checks that the link holds together: the patch or prefab exists, the ViewModel is one, and the mixin is attached to it
  (UIX0018)
* checks that the XML binds at least one member of the mixin where it goes in, so a link cannot outlive the bindings it
  was written for (UIX0019)

## When to write one

Inference covers the common case, a patch binding what your mixin adds. Link the patch when:

* **It binds only members of a game ViewModel.** No mixin adds any of its names, so there is nothing to infer from, and
  each name is reported as UIX0016.
* **Several of your mixins could answer.** Inference takes the ViewModel that has the most of the patch's names, which
  may not be the one you meant.
* **It is your own prefab, reached from nowhere the build can follow.** A replacement for a game prefab, such as
  Diplomacy's `ClansPanel.xml`, is loaded by the game's code. Without a link the analyzer checks it only when every name
  at its root resolves on one of your mixins' ViewModels, one of them added by a mixin.
* **You want the pairing checked.** A link with a mixin is reported when the patch stops binding that mixin's members,
  after a rename or a move of the patch to another screen.
