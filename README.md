# Bannerlord.UIExtenderEx
<p align="center">
  <!--
  <a href="https://github.com/BUTR/Bannerlord.UIExtenderEx" alt="Logo">
  <img src="https://github.com/BUTR/Bannerlord.UIExtenderEx/blob/dev/resources/Butter.png?raw=true" /></a>
  </br>
  -->
  <a href="https://github.com/BUTR/Bannerlord.UIExtenderEx" alt="Lines Of Code">
  <img src="https://tokei.rs/b1/github/BUTR/Bannerlord.UIExtenderEx?category=code" /></a>
  <a href="https://www.codefactor.io/repository/github/butr/bannerlord.uiextenderex"><img src="https://www.codefactor.io/repository/github/butr/bannerlord.uiextenderex/badge" alt="CodeFactor" /></a>
  </br>
  <a href="https://github.com/BUTR/Bannerlord.UIExtenderEx/actions?query=workflow%3ATest"><img src="https://github.com/BUTR/Bannerlord.UIExtenderEx/workflows/Test/badge.svg?branch=dev&event=push" alt="Test" /></a>
  <a href="https://codecov.io/gh/BUTR/Bannerlord.UIExtenderEx"><img src="https://codecov.io/gh/BUTR/Bannerlord.UIExtenderEx/branch/dev/graph/badge.svg" />
   </a>
  </br>
  <a href="https://www.nuget.org/packages/Bannerlord.UIExtenderEx" alt="NuGet Bannerlord.UIExtenderEx">
  <img src="https://img.shields.io/nuget/v/Bannerlord.UIExtenderEx.svg?label=NuGet%20Bannerlord.UIExtenderEx&colorB=blue" /></a>
  <a href="https://butr.github.io/Bannerlord.UIExtenderEx" alt="Documentation">
  <img src="https://img.shields.io/badge/Documentation-%F0%9F%94%8D-blue?style=flat" /></a>
  </br>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="Nexus UIExtenderEx">
  <img src="https://img.shields.io/badge/Nexus-UIExtenderEx-yellow.svg" /></a>  
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="UIExtenderEx">
  <img src="https://img.shields.io/endpoint?url=https%3A%2F%2Fnexusmods-version-pzk4e0ejol6j.runkit.sh%3FgameId%3Dmountandblade2bannerlord%26modId%3D2102" /></a>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="Nexus UIExtenderEx">
  <img src="https://img.shields.io/endpoint?url=https%3A%2F%2Fnexusmods-downloads-ayuqql60xfxb.runkit.sh%2F%3Ftype%3Dunique%26gameId%3D3174%26modId%3D2102" /></a>
  <a href="https://www.nexusmods.com/mountandblade2bannerlord/mods/2102" alt="Nexus UIExtenderEx">
  <img src="https://img.shields.io/endpoint?url=https%3A%2F%2Fnexusmods-downloads-ayuqql60xfxb.runkit.sh%2F%3Ftype%3Dtotal%26gameId%3D3174%26modId%3D2102" /></a>
  </br>
</p>

A library that enables multiple mods to alter standard game interface.

## Installation
This module should be one of the highest in loading order. Ideally, it should be loaded after ``Bannerlord.Harmony`` or ``Bannerlord.ButterLib``.

## For Players
This mod is a dependency mod that does not provide anything by itself. You need to additionaly install mods that use it.

## Quickstart
*See [here](https://github.com/BUTR/Bannerlord.UIExtenderEx/tree/dev/V1Doc) for the PrefabsV1 API documentation.*

Version 2 of the API builds off of the the concepts of the original API, but offers a bit more versatility, and aggregates some of the original prefab types to offer a (hopefully) simpler API.

All UIExtenderEx patch classes must be flagged with a ``PrefabExtensionAttribute``. 
The first parameter is the name of the Movie (the name of the xml file) that your patch targets.
The second parameter is an ``XPath`` used to specify the node you wish to target inside of the targetted Movie.

**For those of you who are unfamiliar with XPath:**
- [Tutorial](https://www.w3schools.com/xml/xpath_intro.asp)
- [Cheatsheet](https://devhints.io/xpath)

### Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch
Patch that adds or replaces a node's attributes. The target node should be specified by the XPath in the ``PrefabExtension``
If the attribute already exists on the target node, it's value will be replaced by the specified value. Otherwise, the new attribute is added with the specified value.

---

**Example of adding multiple attributes:**

```csharp
[PrefabExtension( "ExampleFile", "descendant::Widget[@Id='OptionsScreenWidget']/Children/OptionsTabToggle" )]
internal class AddMultipleAttributesExamplePatch : PrefabExtensionSetAttributePatch
{
    public override List<Attribute> Attributes => new()
    {
        new Attribute( "IsVisible", "@IsDefaultCraftingMenuVisible" ),
        new Attribute( "IsEnabled", "true" )
    };
}
```
```xml
<!-- ExampleFile.xml -->
<!-- Before Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle IsVisible="true"/>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>

<!-- After Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle IsVisible="@IsDefaultCraftingMenuVisible" IsEnabled="true"/>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>
```

### Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch
Versatile patch that can be used to Prepend, Append, Replace (entirely, or while keeping children) or AddAsChild. Insertion type is determined by the ``Type`` Property.

Your class insertion patch class should contain a single Property or Method flagged with one of the attributes inheriting from [PrefabExtensionContentAttribute](https://github.com/BUTR/Bannerlord.UIExtenderEx/blob/dev/src/Bannerlord.UIExtenderEx/Prefabs2/Attributes/PrefabExtensionContentAttribute.cs).
Supported types are the following:
- XmlDocument
- XmlNode
- IEnumerable<XmlNode>
- string (can represent either a file name (``PrefabExtensionFileNameAttribute ``), or Xml (``PrefabExtensionTextAttribute ``))

The Attribute you use will depend on the return type of the method, or the type of the property that it is associated with.

See [PrefabExtensionInsertPatch.cs](https://github.com/BUTR/Bannerlord.UIExtenderEx/blob/dev/src/Bannerlord.UIExtenderEx/Prefabs2/PrefabExtensionInsertPatch.cs) for the full documentation.

---

**Example of prepending the content of an XmlDocument:**

```csharp
[PrefabExtension( "ExampleFile", "descendant::Widget[@Id='OptionsScreenWidget']/Children/OptionsTabToggle" )]
internal class PrependExamplePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Prepend;

    private XmlDocument document;

    public TestPrefabExtensionReplacePatch()
    {
        document = new XmlDocument();
        document.LoadXml("<OptionsTabToggle Id=\"PrependedTabToggle\"><SomeChild/></OptionsTabToggle>");
    }
        
    [PrefabExtensionXmlDocument]
    public XmlDocument GetPrefabExtension() => document;
}
```
```xml
<!-- ExampleFile.xml -->
<!-- Before Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle/>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>

<!-- After Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle Id="PrependedTabToggle">
                    <SomeChild/>
                </OptionsTabToggle>
                <OptionsTabToggle/>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>
```

---

**Example of appending an XmlNode:**

```csharp
[PrefabExtension( "ExampleFile", "descendant::Widget[@Id='OptionsScreenWidget']/Children/OptionsTabToggle" )]
internal class AppendExamplePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    private XmlDocument document;

    public TestPrefabExtensionReplacePatch()
    {
        document = new XmlDocument();
        document.LoadXml("<OptionsTabToggle Id=\"AppendedTabToggle\"/>");
    }
        
    [PrefabExtensionXmlNode]
    public XmlNode GetPatchContent() => document.DocumentElement;
}
```
```xml
<!-- ExampleFile.xml -->
<!-- Before Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle/>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>

<!-- After Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle/>
                <OptionsTabToggle Id="AppendedTabToggle"/>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>
```

---

**Example of adding multiple XmlNodes as children:**

```csharp
[PrefabExtension( "ExampleFile", "descendant::Widget[@Id='OptionsScreenWidget']/Children" )]
internal class AddAsChildrenExamplePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    // When the InsertType is set to InsertType.Child, determines the index the patch should occupy in the target node's child list. 
    // Default is 0 (patch would be the first child).
    public override int Index => 1;

    private List<XmlNode> nodes;

    public TestPrefabExtensionReplacePatch()
    {
        XmlDocument firstChild = new XmlDocument();
        firstChild.LoadXml("<OptionsTabToggle Id=\"InsertedFirstChild\"><Children><InnerChild/></Children></OptionsTabToggle>");
        XmlDocument secondChild = new XmlDocument();
        secondChild.LoadXml("<OptionsTabToggle Id=\"InsertedSecondChild\"/>");

        nodes = new List<XmlNode> {firstChild, secondChild};
    }

    // Just to demonstrate that both Properties and Methods are supported.
    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> Nodes => nodes;
}
```
```xml
<!-- ExampleFile.xml -->
<!-- Before Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle Id="ExistingFirstChild"/>
                <OptionsTabToggle Id="ExistingSecondChild"/>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>

<!-- After Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle Id="ExistingFirstChild">
                <OptionsTabToggle Id="InsertedFirstChild">
                    <Children>
                        <InnerChild/>
                    </Children>
                </OptionsTabToggle>
                <OptionsTabToggle Id="InsertedSecondChild"/>
                <OptionsTabToggle Id="ExistingSecondChild">
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>
```

---

**Example of replacing a node:**

```csharp
[PrefabExtension( "ExampleFile", "descendant::Widget[@Id='OptionsScreenWidget']/Children/OptionsTabToggle" )]
internal class ReplaceNodeExamplePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Replace;

    [PrefabExtensionText]
    public string GetReplacementPatch => "<Widget Id=\"ReplacementNode\"/>";
}
```
```xml
<!-- ExampleFile.xml -->
<!-- Before Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle>
                    <Children>
                        <SomeChild/>
                    </Children>
                </OptionsTabToggle>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>

<!-- After Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <Widget Id="ReplacementNode"/>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>
```

---

**Example of replacing a node while keeping its children:**

```csharp
[PrefabExtension( "ExampleFile", "descendant::Widget[@Id='OptionsScreenWidget']/Children/OptionsTabToggle" )]
internal class ReplaceNodeExamplePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.ReplaceKeepChildren;

    // When the InsertType is set to InsertType.ReplaceKeepChildren, determines which new node should inherit the target node's children.
    // Only applicable when multiple nodes are inserted.
    public override int Index => 1;

    private IEnumerable<XmlNode> nodes;

    [PrefabExtensionXmlNodes]
    public IEnumerable<XmlNode> GetNodes()
    {
        if(nodes is null)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml("<DiscardedRoot><Widget Id=\"FirstChild\"/><Widget Id=\"SecondChild\"/><Widget Id=\"ThirdChild\"/></DiscardedRoot>")
            // We discard the "DiscardedRoot" node by only fetching its children.
            nodes = document.DocumentElement.ChildNodes.Cast<XmlNode>();
        }
        return nodes;
    }
}
```
```xml
<!-- ExampleFile.xml -->
<!-- Before Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle>
                    <Children>
                        <SomeChild/>
                    </Children>
                </OptionsTabToggle>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>

<!-- After Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <Widget Id="FirstChild"/>
                <Widget Id="SecondChild">
                    <Children>
                        <SomeChild/>
                    </Children>
                </Widget>
                <Widget Id="ThirdChild"/>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>
```

Inserting multiple children at the "root" level like in the above example can be tidier by using the "RemoveRootNode" parameter available with the following attribute types:
- ``PrefabExtensionFileNameAttribute``
- ``PrefabExtensionTextAttribute``
- ``PrefabExtensionXmlNodeAttribute``
- ``PrefabExtensionXmlDocumentAttribute``

**Example of using RemoveRootNode. The result will be the same as the example above:**

```csharp
[PrefabExtension( "ExampleFile", "descendant::Widget[@Id='OptionsScreenWidget']/Children/OptionsTabToggle" )]
internal class ReplaceNodeExamplePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.ReplaceKeepChildren;

    public override int Index => 1;

    // Setting "RemoveRootNode" to true.
    [PrefabExtensionText(true)]
    public string GetContent() => "<DiscardedRoot><Widget Id=\"FirstChild\"/><Widget Id=\"SecondChild\"/><Widget Id=\"ThirdChild\"/></DiscardedRoot>";
}
```

---

``PrefabExtensionInsertPatch`` also supports fetching and inserting xml from a file inside of your module's GUI folder.
The biggest advantage of doing this is being able to perform live debugging on your injected patch!

**Example of appending the content of a file using ``PrefabExtensionFileNameAttribute``:**

```csharp
[PrefabExtension( "ExampleFile", "descendant::Widget[@Id='OptionsScreenWidget']/Children/OptionsTabToggle" )]
internal class ReplaceNodeExamplePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    // The file should have an extension of type .xml, and be located inside of the GUI folder of your module.
    // You can include or omit the extension type. I.e. both of the following would work:
    //   ExampleFileInjectedPatch
    //   ExampleFileInjectedPatch.xml
    [PrefabExtensionFileName]
    public string PatchFileName => "ExampleFileInjectedPatch";
}
```
```xml
<!-- ExampleFileInjectedPatch.xml -->
<Widget Id="InjectedWidget">
    <Children>
        <SomeOtherChild/>
    </Children>
</Widget>
```
```xml
<!-- ExampleFile.xml -->
<!-- Before Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle>
                    <Children>
                        <SomeChild/>
                    </Children>
                </OptionsTabToggle>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>

<!-- After Patch -->
<Prefab>
    <Window>
        <OptionsScreenWidget Id="Options">
            <Children>
                <OptionsTabToggle>
                    <Children>
                        <SomeChild/>
                    </Children>
                </OptionsTabToggle>
                <Widget Id="InjectedWidget">
                    <Children>
                        <SomeOtherChild/>
                    </Children>
                </Widget>
            </Children>
        </OptionsScreenWidget>
    </Window>
</Prefab>
```

---

## ViewModelMixin

In order to add data to the prefab, you need to add properties to the target datasource class, this is done by making a _mixin_ class, inheriting from `BaseViewModelMixin<T>` and marking it with `ViewModelMixin` attribute. This class will be mixed in to the target view model `T`, making fields and methods accessible in the prefab:

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

To use the `OnRefresh` overload you will need to specify for UIExtenderEx the underlying method that acts as the conceptual 'Refresh' method in the `ViewModel`.  
For example, `MapInfoVM` has a method `Refresh`.  
If such method exists, specify it in the `ViewModelMixin` like this:
```csharp
[ViewModelMixin("Refresh")] // or [ViewModelMixin(nameof(MapInfoVM.Refresh))] // if the method is public
public class MapInfoMixin : BaseViewModelMixin<MapInfoVM>
```

### Examples
* [Bannerlord.MBOptionScreen](https://github.com/Aragas/Bannerlord.MBOptionScreen/tree/dev/src/MCM.UI/UIExtenderEx)
* [Yet Another Party Organiser](https://github.com/tbeswick96/BannerlordYetAnotherPartyOrganiser)
