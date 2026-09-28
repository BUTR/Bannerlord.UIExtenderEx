# Analyzers

`Bannerlord.UIExtenderEx.Analyzers` checks your ViewModel mixins and your prefab XML while you build. Each rule is a mistake UIExtenderEx
would otherwise let through without a word at runtime - a binding that reaches the wrong member, an `OnRefresh` that
never comes - or one that crashes a screen when it opens.

It checks your mixins against the game's ViewModels and against each other, for the game version you build against. It
cannot see other mods: names two mods both add are reported by UIExtenderEx when the second one registers.

Most rules come with a [code fix](#code-fixes) that makes the change for you.

## Installing

Add the package next to your UIExtenderEx reference. It is a separate package, so it works however you reference
UIExtenderEx, including with `IncludeAssets="compile"`:

```xml
<PackageReference Include="Bannerlord.UIExtenderEx.Analyzers" Version="$(UIExtenderExVersion).*" PrivateAssets="all" />
```

The package is released on its own, whenever the analyzers change. Its version is the UIExtenderEx version it was built
with, followed by its own build number: `2.13.3.17`. `$(UIExtenderExVersion).*` takes the newest build for the
UIExtenderEx you reference.

It needs the .NET 8 SDK or Visual Studio 2022 17.8 or later. It adds nothing to your mod's output. It checks nothing in a
project that does not reference UIExtenderEx.

It works with UIExtenderEx 2.x and later, and checks your code against what the version you reference does: where two
versions behave differently, the rule says so below. The rules for `[BUTRViewModelOverride]` and `[BUTRUnsafeAccessor]`
(UIX0008 to UIX0010) need UIExtenderEx 3.0, which has those attributes. [`[PrefabLink]`](../v2/PrefabLink.md) comes with
this package when your UIExtenderEx does not have it.

A new version of the package can bring new rules. If you build with warnings as errors, pin a build, such as
`Version="2.13.3.17"`, and update it when you choose to, not together with UIExtenderEx.

The prefab rules read the XML your project embeds and any XML under a `GUI` folder of the project; the package hands
those to the compiler. Set `<UIExtenderExAnalyzePrefabs>false</UIExtenderExAnalyzePrefabs>` to leave them out.

### Building for several game versions

Every message starts with the game version your project builds against:

```text
ModOptionsView.xml(44,139): warning UIX0012: [v1.4.8] 'ScrollablePanel' has no attribute 'HorizontalAlightment'; the loader drops it without a message
```

A module that `Bannerlord.BUTRModule.Sdk` builds for several game versions is compiled once per version, each time
against that version's game. The same warning can come from several of those builds, and one only some of them give
holds for those versions only: a patch whose node a version does not have is not applied there, and the player sees
UIExtenderEx's message that it failed. Keep such a patch out of those versions with the SDK's version symbols, such as
`#if !v129`. Your XML is shared by every version, so a binding one version's ViewModel lacks has to be supplied there,
by a mixin, or left to show nothing.

The version is `$(GameVersion)`, which the SDK and `Bannerlord.BuildResources` set. Set
`<UIExtenderExGameVersion>` to name another; with neither set, messages carry no version.

With [`Bannerlord.ReferenceAssemblies.GUI.v2.All`](#every-supported-version-at-once) referenced, each build checks your
patches against every version you support, and a patch that fails in some of them is reported once, naming them
(UIX0024).

## Rules

| Rule | Severity | What it finds | Code fix |
| --- | --- | --- | --- |
| [UIX0001](#uix0001) | Warning | A mixin member that replaces a member of the ViewModel it extends |  |
| [UIX0002](#uix0002) | Warning | Two of your mixins adding the same name to one ViewModel |  |
| [UIX0003](#uix0003) | Warning | A refresh method name UIExtenderEx cannot find | A name the ViewModel has |
| [UIX0004](#uix0004) | Warning | A mixin on an abstract ViewModel, which UIExtenderEx does not register | `handleDerived: true` |
| [UIX0005](#uix0005) | Warning | A mixin whose ViewModel UIExtenderEx cannot tell |  |
| [UIX0006](#uix0006) | Error | A mixin UIExtenderEx cannot create | The constructor it needs |
| [UIX0007](#uix0007) | Warning | A marked member that is not public | Made public |
| [UIX0008](#uix0008) | Warning | A `[BUTRViewModelOverride]` that matches no method of the ViewModel |  |
| [UIX0009](#uix0009) | Warning | A `[BUTRUnsafeAccessor]` stub whose member cannot be found |  |
| [UIX0010](#uix0010) | Warning | A `[BUTRUnsafeAccessor]` stub that can be inlined | `NoInlining` |
| [UIX0011](#uix0011) | Error | Prefab XML that is not well-formed |  |
| [UIX0012](#uix0012) | Warning | An attribute the widget does not have | An attribute it has |
| [UIX0013](#uix0013) | Warning | An attribute value the loader cannot convert | A value it can |
| [UIX0014](#uix0014) | Warning | A parameter your prefab does not use | A parameter it uses |
| [UIX0015](#uix0015) | Warning | A binding or command the ViewModel does not have | A member it has |
| [UIX0016](#uix0016) | Warning | A patch binding a member none of your mixins' ViewModels has | A member one has |
| [UIX0017](#uix0017) | Error | An insert patch's content member UIExtenderEx cannot read | Public, instance, or the attribute its type fits |
| [UIX0018](#uix0018) | Warning | A `[PrefabLink]` that does not hold together |  |
| [UIX0019](#uix0019) | Warning | Linked XML that binds none of the linked mixin's members | The mixin taken out of the link |
| [UIX0020](#uix0020) | Warning | A patch XPath that matches no node of the prefab | |
| [UIX0021](#uix0021) | Warning | A patch XPath that matches several nodes | |
| [UIX0022](#uix0022) | Warning | A `[PrefabLink]` that disagrees with the game | |
| [UIX0023](#uix0023) | Error | A patch XPath that is not valid | |
| [UIX0024](#uix0024) | Warning | A patch XPath that fails in some of the supported game versions | |

UIX0002 and the prefab rules UIX0011 to UIX0016 and UIX0018 to UIX0024 are reported on a full build, not while you type:
they need the whole mod at once. In Visual Studio they show in the Error List after a build, or while you type with full
solution analysis turned on.

To silence a rule for one member, use `#pragma warning disable UIX0001`. For the whole project, set
`dotnet_diagnostic.UIX0001.severity = none` in `.editorconfig`.

## Code fixes

The fixes come in the same package; there is nothing more to install. Your editor offers them on the warning, as a
quick action (`Ctrl+.` in Visual Studio, `Alt+Enter` in Rider). Each rule's section below says what its fix does.

Two kinds of fix:

* **The fix is known.** UIX0004, UIX0006, UIX0007, UIX0010, UIX0017 and UIX0019 make one change the rule asks for. These
  can be applied to a whole document, project or solution at once with the editor's "Fix all".
* **The fix is a name.** UIX0003 and UIX0012 to UIX0016 report a name that is misspelled, and offer up to three names in
  its place: those within a few letters of it, ignoring case, closest first. A name that differs only in case comes
  first. When nothing is that close, there is no fix. Each offer puts in a different name, so these have no "Fix all".

The XML fixes edit the XML where it is written: a prefab file, or a string literal in a patch class. An editor offers a
fix in an `.xml` file only if it runs Roslyn code fixes on files other than C#; the XML in C# literals can be fixed in
any editor. Where the analyzer could not map a report to the exact characters in a literal - a raw string literal
(`"""..."""`) is reported on as a whole - the fix is offered only when the name appears once in that literal.

An editor shows a fix only for a warning it has worked out itself. The rules reported on a full build, UIX0002 and the
prefab rules, are worked out while you type only with full solution analysis turned on; without it they show in the
Error List after a build, with no fix to click.

Rules without a fix leave a choice to you: UIX0001 a new name, which your XML has to use too, or an override of the
method; UIX0002 a new name; UIX0005,
UIX0008, UIX0009, UIX0018 and UIX0022 what the code was meant to reach; UIX0011, UIX0020, UIX0021, UIX0023 and UIX0024
where the XML or the XPath went wrong.

## UIX0001

**A mixin member replaces a member of the ViewModel it extends.**

A mixin's `[DataSourceProperty]` and `[DataSourceMethod]` members are added to the ViewModel under their names, over
the ViewModel's own. Every binding and command of that name in a prefab then reaches the mixin's. The game's own code
does not: it goes on calling the ViewModel's, so a replaced command runs from the button but not from a hotkey or
anything else that calls the method. Private members of the ViewModel count: the table holds them too.

```csharp
[ViewModelMixin]
public sealed class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
{
    public OptionsVMMixin(OptionsVM vm) : base(vm) { }

    [DataSourceMethod] public void ExecuteDone() { } // UIX0001: OptionsVM has ExecuteDone
}
```

Rename the member, with a prefix of your mod's (`MyMod_ExecuteDone`). With `handleDerived`, every ViewModel derived
from the one you extend is checked.

When replacing a method is the point, take it over with [`[BUTRViewModelOverride]`](#uix0008) (UIExtenderEx 3.0 and
later) instead of adding a command of the same name. The override replaces the method itself, so it runs for every caller: the button, a hotkey,
and the game's own code. It gets the original as its last parameter, to call before or after its own work or not at
all, and overrides from several mods chain rather than the last one winning:

```csharp
[ViewModelMixin]
public sealed class OptionsVMMixin : BaseViewModelMixin<OptionsVM>
{
    public OptionsVMMixin(OptionsVM vm) : base(vm) { }

    [BUTRViewModelOverride(nameof(OptionsVM.ExecuteDone))]
    private void ExecuteDone(Action original)
    {
        SaveMyModSettings();
        original();
    }
}
```

Only methods that return `void` and take no `ref` or `out` parameters can be taken over. A property has no such
counterpart: rename it.

## UIX0002

**Two of your mixins add the same name to one ViewModel.**

Both write the name into the ViewModel's binding table, and bindings reach only the one written last. Which is last
depends on the order the types come out of your assembly, which nothing guarantees. A mixin with `handleDerived` meets
the mixins on every ViewModel derived from its own. Give each member a name of its own.

## UIX0003

**The refresh method named on `[ViewModelMixin]` cannot be found on the ViewModel.**

UIExtenderEx looks the name up on the ViewModel and its base types. When the name is overloaded, only an overload taking
no parameters is used. If nothing is found, UIExtenderEx 3.0 and later register the mixin without the hook, so
`OnRefresh` never runs. Older versions throw from `UIExtender.Register`: the mixin and every type of your mod after it
are not registered.

```csharp
[ViewModelMixin("RefreshValuez")] // UIX0003: a typo
```

Use `nameof(TheViewModel.RefreshValues)` so the compiler checks the name for you.

**Fix:** *Use 'RefreshValues'*, for each method of the ViewModel close to the name that UIExtenderEx can hook. It
writes `nameof(TheViewModel.Method)` when the mixin can see the method, and the name as a string when it cannot, as
for a private method of a game ViewModel.

## UIX0004

**The mixin extends an abstract ViewModel without `handleDerived`.**

A mixin is attached to instances whose type is exactly the ViewModel it extends, and an abstract ViewModel has none -
`ViewModel` itself included. The mixin never runs: UIExtenderEx 3.0 and later do not register it, and say so in the log;
older versions register it without a word. Extend the concrete ViewModel, or pass `handleDerived: true` to reach every
type derived from it.

**Fix:** *Set handleDerived: true* adds `handleDerived: true` to `[ViewModelMixin]`, or turns a `false` already
there into `true`. Take it when the mixin is meant for every ViewModel derived from the abstract one; when it is meant
for one of them, change the type argument instead.

## UIX0005

**UIExtenderEx cannot tell which ViewModel the mixin extends.**

UIExtenderEx takes the ViewModel from the `BaseViewModelMixin<TViewModel>` the mixin derives from, however many shared
base classes of your own are in between. A mixin that implements `IViewModelMixin` itself instead has the ViewModel
taken from the first type argument of the first type, from the mixin up, that implements it - which may not be the
ViewModel. Derive from `BaseViewModelMixin<TViewModel>`.

## UIX0006

**The mixin cannot be created.**

UIExtenderEx creates a mixin at the end of the ViewModel's constructor, through a public constructor taking the
ViewModel as its only argument. If the mixin is abstract, generic, or has no such constructor, the exception leaves the
ViewModel's constructor and the screen that was opening fails. That is why this rule is an error.

```csharp
public MyMixin(MyViewModel vm) : base(vm) { }
```

**Fix:** depends on why the mixin cannot be created:

* abstract: *Make 'MyMixin' not abstract*;
* a constructor taking the ViewModel that is not public: *Make the constructor public*;
* no such constructor: *Add a constructor taking 'MyViewModel'*, which adds `public MyMixin(MyViewModel vm) : base(vm) { }`
  after the fields.

A generic mixin has no fix: which type argument it should have is yours to say.

## UIX0007

**A member marked `[DataSourceProperty]` or `[DataSourceMethod]` is not public.**

Only public members of a mixin are added to the ViewModel; others are skipped without a message. A public property with
a `private set` is public and fine.

**Fix:** *Make 'Name' public* replaces the member's accessibility with `public`.

## UIX0008

**A `[BUTRViewModelOverride]` method matches no method of the ViewModel.** UIExtenderEx 3.0 and later.

An override takes the ViewModel method's parameters followed by the original, a delegate taking the same parameters,
and returns `void`, as the ViewModel method has to. The ViewModel method is found by the name on the attribute and by
those parameters, whatever its accessibility. UIExtenderEx leaves out an override it cannot match, and the ViewModel
method runs as if it were not there.

```csharp
[BUTRViewModelOverride(nameof(OptionsVM.ExecuteDone))]
private void ExecuteDone(Action original) => _modOptions.ExecuteDoneInternal(false, original);
```

## UIX0009

**A `[BUTRUnsafeAccessor]` stub names no member UIExtenderEx can find.** UIExtenderEx 3.0 and later.

The stub is `static` and names its member by its signature: the instance as the first parameter for an instance member,
the type on the attribute for a static one, the member's parameters and return type, and a `ref` return for a field. A
game update that renames the member shows here when you build against it. A stub UIExtenderEx cannot resolve keeps its
own body, so calling it throws.

```csharp
[BUTRUnsafeAccessor(BUTRAccessorKind.Field, Name = "_categories")]
[MethodImpl(MethodImplOptions.NoInlining)]
private static ref List<ViewModel> Categories(OptionsVM instance) => throw new NotImplementedException();
```

## UIX0010

**A `[BUTRUnsafeAccessor]` stub is not marked `NoInlining`.** UIExtenderEx 3.0 and later.

UIExtenderEx replaces the stub's body when your assembly is registered. A caller compiled before that could have copied
the original body, which throws, into itself. Mark the stub `[MethodImpl(MethodImplOptions.NoInlining)]`.

**Fix:** *Mark the stub NoInlining* adds `[MethodImpl(MethodImplOptions.NoInlining)]`, with the using when the file has
none. On a stub that has `[MethodImpl]` already, it adds `NoInlining` to the flags there, or replaces
`AggressiveInlining` with it.

## Which ViewModel your XML binds

The binding rules need to know the ViewModel at each point of your XML.

* **A patch** binds whatever the game's movie binds where it goes in, which the build cannot read. The analyzer takes it
  from your mixins: the ViewModel one of them extends on which the patch's names resolve, with at least one name added
  by a mixin. MCM's patches bind `{ModOptions}` and `@DescriptionWidth`, which its `OptionsVM` mixin adds, so they are
  checked against `OptionsVM`.
* **Link it yourself** with [`[assembly: PrefabLink]`](../v2/PrefabLink.md) when a patch binds only members of a game
  ViewModel, or when several of your mixins could answer:
  `[assembly: PrefabLink(typeof(OptionsPatch), typeof(OptionsVM), typeof(OptionsVMMixin))]`. A link wins over what the
  mixins suggest.
* **Your own prefab** that nothing of yours loads, such as a replacement for a game prefab, is checked against the
  ViewModel a link names: `[assembly: PrefabLink("ClansPanel", typeof(ClanManagementVM))]`.
* **From there** the scope follows `DataSource` paths, list item templates, parameters, and your own prefabs by the name
  you register them under (`WidgetFactoryManager.CreateAndRegister("Name", ...)`) or their file name.
* **A movie you load yourself** with `LoadMovie("Name", viewModel)` is checked against the type of the ViewModel you pass.

Below a point the analyzer cannot tell - a game prefab it does not have, a value it cannot read - nothing is checked.

## What the prefab rules can read

The analyzer reads your XML without running your code, so it takes what is written out as a string literal and nothing
built at runtime:

| Source | Read when |
| --- | --- |
| A prefab file | It is one of the XML files the package hands the compiler (see [Installing](#installing)) and its root is `<Prefab>` |
| A prefab's name | The file is registered with `WidgetFactoryManager.CreateAndRegister("Name", ...)` or `Register("Name", ...)` and a `.xml` string literal among the arguments names the file; otherwise its file name |
| A movie you load | `LoadMovie("Name", viewModel)` with the name as a constant |
| `[PrefabExtensionFileName]` | The member returns a string literal naming one of those files |
| `[PrefabExtensionText]` | The member returns a string literal |
| `[PrefabExtensionXmlNode]`, `[PrefabExtensionXmlNodes]` | The patch class passes a string literal to `LoadXml` |
| `PrefabExtensionSetAttributePatch` | Its attributes are `new Attribute("Name", "Value")` with two literals |
| A v1 patch (`Bannerlord.UIExtenderEx.Prefabs`) | Its XPath always. Its XML when the class passes a string literal to `LoadXml`, or is a `ModulePrefabExtensionInsertPatch` or `EmbedPrefabExtensionInsertPatch` naming one of your files; a v1 set-attribute patch's `Attribute` and `Value` when each returns a literal |

A widget tag is matched by class name against every class derived from `Widget` that the build can see: the game's,
your mod's, and those of mods you reference. A tag that is neither such a class nor a prefab of yours is not checked,
and neither are its attributes. The game's own prefabs are read only when you reference their GUI packages; see
[Checking against the game's prefabs](#checking-against-the-games-prefabs). Without them a game prefab your XML uses is
not checked inside, and a scope that runs through one is lost.

## Checking against the game's prefabs

A patch lands in the game's own XML, which your project does not contain. The `Bannerlord.ReferenceAssemblies.GUI.v2`
packages describe it, one per game version from v1.0.0 on, generated from each Steam build next to the reference assemblies: every
module's prefabs, the ViewModel each movie is loaded with, and the game's ViewModel types. They carry no file of the
game's: each prefab is a tree of its elements and attributes, from which the analyzer rebuilds the document the game
loads. Reference them next to the analyzers, at the version you build against:

```xml
<PackageReference Include="Bannerlord.ReferenceAssemblies.GUI.v2" Version="$(GameVersion).*" PrivateAssets="all" />
<PackageReference Include="Bannerlord.ReferenceAssemblies.GUI.v2.NavalDLC" Version="$(GameVersion).*" PrivateAssets="all" />
```

For an early access version (`e1.x`), reference `Bannerlord.ReferenceAssemblies.GUI.v2.EarlyAccess` instead; the DLC came
later and has no early access package. A beta is published with a `-beta` suffix, which `$(GameVersion).*` does not
match: use `$(GameVersion).*-*` to build against one.

They add nothing to your compilation or output. The DLC package is optional: reference it to have your patches
checked for players who own War Sails as well as for those who do not; it exists from v1.3.4 on. Set
`<UIExtenderExAnalyzeGamePrefabs>false</UIExtenderExAnalyzeGamePrefabs>` to leave them out without removing them.

The analyzer reads the packages' format 2, which the `.v2` ids carry. A package in another format is left out: the
checks against the game's prefabs stay silent.

With them, each patch is applied to the prefab it names the way UIExtenderEx applies it, with `SelectSingleNode` on
the prefab's document:

* **The XPath has to select a node** (UIX0020), and only one (UIX0021). The prefab is your own when your mod has one of
  that name, which is the one the game loads; otherwise the game's. A node another patch of yours inserts counts as
  there, and so does any node when another patch of yours changes that prefab in a way the build cannot read.
* **Each configuration is checked:** the game without the DLC, and with each DLC package you reference. A DLC ships its
  own copy of some prefabs, and replaces some screens with its own, so an XPath can hold in one and not the other. The
  report says which.
* **The game's scope replaces the guess.** The analyzer follows the game's XML from the movie's ViewModel down to the
  node: `DataSource` paths, item templates, and prefabs used by tag with the parameters they are handed. Content
  inserted as a child, and a set-attribute patch's bindings, bind inside the node; content placed before, after or
  instead of it binds around it. Your patch's bindings are checked against that ViewModel (UIX0015), and a
  `[PrefabLink]` naming another one is reported (UIX0022).
* **A set-attribute patch's attributes** are checked against the widget the XPath selects (UIX0012, UIX0013).

Mods built with `Bannerlord.BUTRModule.Sdk` compile once per game version in their `supported-game-versions.txt`, and
restore packages at `$(GameVersion).*` for each. With the references above, every patch is checked against every
supported version, and a version where an XPath no longer holds shows as a warning in that version's build.

### Every supported version at once

The per-version packages hold one game version each, and NuGet restores one version of a package per project, so a
build sees only the version it compiles against. `Bannerlord.ReferenceAssemblies.GUI.v2.All` holds the newest build of
every release version, base game and War Sails, in one package of about 1.3 MB: each distinct piece of the data is
stored once. Reference it next to, or instead of, the per-version packages:

```xml
<PackageReference Include="Bannerlord.ReferenceAssemblies.GUI.v2.All" Version="*" PrivateAssets="all" />
```

Its version is the date it was built, `2026.9.28.57`; `*` takes the newest.

**Which versions are checked.** Those in `supported-game-versions.txt`, which `Bannerlord.BUTRModule.Sdk` reads. Set
`<UIExtenderExGameVersions>v1.2.12;v1.3.4;v1.4.8</UIExtenderExGameVersions>` to name them otherwise. With neither, the
version you build against. A supported version the package does not have is skipped. For a version a per-version
package you reference also has, that package is used: it is the build you compile against.

**What is checked in each.** Where the XPath lands: UIX0020 and UIX0021, in every configuration of every version. A
finding that holds for all of them is reported under its own rule; one that holds for some only is UIX0024, naming them,
and versions in a row read as a range:

```text
Page.cs(12,33): warning UIX0024: [v1.4.8] In v1.0.0 to v1.3.15 only: 'descendant::*[@Id='CurrentOptionExtraInformationWidget']' matches no node of 'Options'; the patch is not applied
```

The rest is checked against the version you build against: your patch's bindings (UIX0015), the `[PrefabLink]`
(UIX0022), and a set-attribute patch's attributes (UIX0012, UIX0013). These read the game's types from your
compilation, which references that version's assemblies.

**Once across the SDK's builds.** Each of the SDK's builds per version sees every version, so each would report the
same finding. Only the build of the newest version the finding holds for reports it. A build outside the SDK's loop,
such as the one in your IDE, reports everything.

What it does not do:

* **A prefab neither the game nor your mod has,** another mod's, is not checked.
* **Your patch is checked against the game's ViewModel as your compilation sees it.** A ViewModel of a module you do
  not reference is not checked, and neither is a node the game reaches with several different ViewModels.
* **A binding a subclass of the ViewModel has passes,** as it does elsewhere: a property declared as a base type may
  hold the subclass. So a member only the DLC's subclass has is not reported for the game without the DLC.

## UIX0011

**Prefab XML is not well-formed.**

A patch's XML that is not well-formed throws when the patch is created, which fails the registration of your mod's UI.
A prefab file that is not well-formed fails when the game loads it.

## UIX0012

**The widget has no such attribute.**

An attribute names a public property of the widget's class. One it does not have - usually a misspelling - is dropped
when the prefab loads, without a message, and the widget keeps its default. Dotted attributes are checked at their first
part: `Brush.Color` on a plain `Widget` is reported, because only `BrushWidget` and its subclasses have a `Brush`.

```xml
<ScrollablePanel HorizontalAlightment="Left" />  <!-- UIX0012: HorizontalAlignment -->
```

**Fix:** *Change to 'HorizontalAlignment'*, for each public property of the widget close to the name. Only the
first part of a dotted attribute is replaced.

## UIX0013

**The attribute's value does not fit its type.**

The loader converts the text: an enum by member name, a number by parsing, a bool by comparing with `"true"`. A name that
is not a member, or text that is not a number, fails the attribute. An enum also takes a number, or several members
separated by commas. Any bool other than `"true"` reads as false: `IsVisible="True"` hides the widget.

Only a literal value on a single-part attribute is checked; a binding, a constant, a parameter or a dotted attribute is
not.

**Fix:** *Change to 'Center'*, for each member of the enum close to the value. For a bool, `True` becomes `true`,
`FALSE` becomes `false`, and anything else is offered both. A number that does not parse has no fix.

## UIX0014

**Your prefab has no such parameter.**

`Parameter.Name` on your prefab's tag reaches the attributes inside it written `*Name`. A name the prefab neither declares
under `<Parameters>` nor reads goes nowhere.

**Fix:** *Change to 'Name'*, for each parameter the prefab declares or reads that is close to the name.

## UIX0015

**The ViewModel has no such member.**

A binding (`@Name`), a step of a `DataSource` path (`{Name}`) or a command (`Command.Click="Name"`) names a member of the
ViewModel at that point. Neither the ViewModel, the types derived from it, nor your mixins have it. Usually a
misspelling; if another mod's mixin is meant to add it, the build cannot know.

**Fix:** *Change to 'Name'*, for each member close to the name that a binding (or for a command, a command) at that
point reaches: the ViewModel's own, those of the types derived from it, and those your mixins add. Only the misspelled
name is replaced; the rest of a `DataSource` path stays.

## UIX0016

**A patch binds a member none of your mixins' ViewModels has.**

Without a [`[PrefabLink]`](../v2/PrefabLink.md), a patch's ViewModel is taken from your mixins. A name that none of their
ViewModels answers is a misspelling, or a member of a game ViewModel no mixin of yours extends: link the patch to that
ViewModel.

```csharp
[PrefabExtension("Options", "descendant::Widget[@Id='DescriptionsRightPanel']")]
internal sealed class Width : PrefabExtensionSetAttributePatch
{
    public override List<Attribute> Attributes => [new Attribute("SuggestedWidth", "@DescriptonWidth")]; // UIX0016
}
```

**Fix:** *Change to 'Name'*, for each member close to the name on any ViewModel your mixins extend. When the patch
binds a game ViewModel no mixin of yours extends, the fix is a `[PrefabLink]` instead, which you write.

## UIX0017

**An insert patch's content member UIExtenderEx cannot read.**

UIExtenderEx looks for the member with a content attribute among the patch's public members, and reads it as a
parameterless instance method, or an instance property, returning the type the attribute names:

| Attribute | Type |
| --- | --- |
| `[PrefabExtensionFileName]`, `[PrefabExtensionText]` | `string` |
| `[PrefabExtensionXmlNode]` | `XmlNode`, or a type derived from it such as `XmlDocument` |
| `[PrefabExtensionXmlNodes]` | `IEnumerable<XmlNode>`, or a type that converts to it such as `List<XmlNode>` |
| `[PrefabExtensionXmlDocument]` | `XmlDocument`; from UIExtenderEx 3.0, which makes it obsolete, what `[PrefabExtensionXmlNode]` takes |

A member that is not public, is static, takes parameters, is generic, has no getter, or has another type fails the
patch when your UI is registered.

```csharp
[PrefabExtension("Options", "descendant::Widget[@Id='DescriptionsRightPanel']")]
internal sealed class Panel : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    [PrefabExtensionXmlNode]
    public string GetContent() => "<Widget />"; // UIX0017: use [PrefabExtensionText] for a string
}
```

**Fix:** depends on why the member cannot supply the content:

* not public: *Make 'GetContent' public*;
* static: *Make 'GetContent' an instance member*;
* of another type: *Use [PrefabExtensionText]*, for each content attribute the member's type fits - for a `string`,
  `[PrefabExtensionText]` and `[PrefabExtensionFileName]`, as only you know whether it is XML or a file name.

A member that takes parameters, is generic, or has no getter has no fix.

## UIX0018

**A `[PrefabLink]` does not hold together.**

A link names a prefab patch of your mod, or a prefab of yours by the name it is registered under or its file name; the
ViewModel its XML binds where it goes in; and optionally a mixin attached to that ViewModel, one extending it or, with
`handleDerived`, one extending a base of it. A patch linked twice has to be linked to the same ViewModel both times. A
link that does not hold together is left out, and the patch or prefab is checked as if it had none.

```csharp
[assembly: PrefabLink(typeof(OptionsPatch), typeof(OptionsVM), typeof(InventoryVMMixin))] // UIX0018: InventoryVMMixin extends SPInventoryVM, not OptionsVM
```

## UIX0019

**Linked XML binds none of the linked mixin's members.**

A link naming a mixin says the XML binds what that mixin adds. XML that binds none of the mixin's members where it goes
in - at the XPath for a patch, at the root for a prefab - is linked to the wrong mixin, or the link outlived the bindings
it was written for, after a rename or after the patch moved to another screen.

```csharp
[assembly: PrefabLink(typeof(TitlePatch), typeof(OptionsVM), typeof(OptionsVMMixin))] // UIX0019: TitlePatch binds only OptionsVM's own @Title
```

Link the patch to the ViewModel alone when it binds only the ViewModel's own members.

**Fix:** *Link to the ViewModel alone* takes the mixin out of the link. Take it when the XML is meant to bind only the
ViewModel's own members; when it is meant to bind the mixin's, the XML is what needs changing.

## UIX0020

**A patch's XPath matches no node of the prefab.**

UIExtenderEx applies the patch at the first node the XPath selects in the prefab the patch names. When it selects
nothing, the patch is skipped, and the player sees a message in game. After a game update this is how
`descendant::Widget[@Id='DescriptionsRightPanel']` breaks. Checked against your own prefab of that name, or, with the
[GUI packages](#checking-against-the-games-prefabs) referenced, against the game's, without and with each DLC:

```text
'descendant::ListPanel[@Id='Panel']' matches no node of 'HostMovie' (with NavalDLC); the patch is not applied
```

A node another of your patches inserts into the same prefab counts as there. So does any node when another of your
patches changes the prefab in a way the build cannot read: XML built at runtime, a file it does not have, a
`CustomPatch`. Another mod's is unknown to the build.

The v1 patches in `Bannerlord.UIExtenderEx.Prefabs` are checked the same way, except `CustomPatch<XmlDocument>`, which
is handed the whole prefab and not the XPath.

## UIX0021

**A patch's XPath matches several nodes of the prefab.**

UIExtenderEx patches only the first. It is the one you meant as long as nothing comes before it; a game update that
adds a matching node earlier in the prefab moves the patch without a word. A position (`Children/*[5]`) is fragile
the same way. Narrow the XPath, by an `Id` where the node has one.

## UIX0022

**A `[PrefabLink]` names another ViewModel than the game binds where the patch goes in.**

With the [GUI packages](#checking-against-the-games-prefabs) referenced, the analyzer knows the ViewModel at the node
the patch's XPath selects. A link naming a ViewModel that is neither that one nor a base or subclass of it is linked to
the wrong screen, or the screen changed. The link is still used to check the patch; the message names the game's
ViewModel, so you can tell which is right.

## UIX0023

**A patch's XPath is not valid.**

UIExtenderEx selects the node with `SelectSingleNode`, which throws for an XPath that does not parse, or that does not
select nodes (`count(//Widget)`). It throws too for a patch whose XPath is `null`, `[PrefabExtension("Options", null)]`: the
patch is handed an empty one. The patch then fails when the movie loads. This needs no GUI package.

## UIX0024

**A patch's XPath fails in some of the game versions you support.**

With [`GUI.v2.All`](#every-supported-version-at-once) referenced, each patch is applied to the game's prefab in every
version you support. When the XPath matches no node (UIX0020), or several (UIX0021), in some of those versions only,
it is reported here, naming them:

```text
In v1.0.0 to v1.3.15 only: 'descendant::*[@Id='CurrentOptionExtraInformationWidget']' matches no node of 'Options'; the patch is not applied
```

In those versions the patch is not applied, and the player sees UIExtenderEx's message that it failed. Leave the patch
out of those versions' builds with the SDK's version symbols (`v1315` is defined in the build for v1.3.15), or target
a node every version has.

It is a rule of its own so that you can weigh it apart from the ones that hold everywhere: a patch meant for newer
versions only is not a mistake. `dotnet_diagnostic.UIX0024.severity = suggestion` in `.editorconfig` lowers it to a
suggestion, and `none` turns it off, without touching UIX0020 and UIX0021.
