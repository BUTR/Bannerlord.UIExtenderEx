### [``PrefabExtensionInsertPatch``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch)  
Versatile patch that can be used to Prepend, Append, Replace (entirely, or while keeping children) or AddAsChild. Insertion type is determined by the [``Type``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch#collapsible-Bannerlord_UIExtenderEx_Prefabs2_PrefabExtensionInsertPatch_Type) Property.

Your class insertion patch class should contain a single Property or Method flagged with one of the attributes inheriting from [``PrefabExtensionContent``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionContentAttribute).
Supported types are the following:
- [``XmlDocument``](xref:System.Xml.XmlDocument)
- [``XmlNode``](xref:System.Xml.XmlNode)
- [``IEnumerable<XmlNode>``](xref:System.Collections.Generic.IEnumerable`1)
- string (can represent either a file name ([``PrefabExtensionFileName``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute)), or Xml ([``PrefabExtensionText``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionTextAttribute)))

The Attribute you use will depend on the return type of the method, or the type of the property that it is associated with.

See [PrefabExtensionInsertPatch.cs](https://github.com/BUTR/Bannerlord.UIExtenderEx/blob/dev/src/Bannerlord.UIExtenderEx/Prefabs2/PrefabExtensionInsertPatch.cs) for the full documentation.

---

**Example of prepending the content of an XmlDocument:**

```csharp
[PrefabExtension("ExampleFile", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle")]
internal class PrependExamplePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Prepend;

    private XmlDocument document;

    public PrependExamplePatch()
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
[PrefabExtension("ExampleFile", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle")]
internal class AppendExamplePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    private XmlDocument document;

    public AppendExamplePatch()
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
[PrefabExtension("ExampleFile", "descendant::OptionsScreenWidget[@Id='Options']/Children")]
internal class AddAsChildrenExamplePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    // When the InsertType is set to InsertType.Child, determines the index the patch should occupy in the target node's child list. 
    // Default is 0 (patch would be the first child).
    public override int Index => 1;

    private List<XmlNode> nodes;

    public AddAsChildrenExamplePatch()
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
[PrefabExtension("ExampleFile", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle")]
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
[PrefabExtension("ExampleFile", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle")]
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
- [``PrefabExtensionFileName``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute)
- [``PrefabExtensionText``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionTextAttribute)
- [``PrefabExtensionXmlNode``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionXmlNodeAttribute)
- [``PrefabExtensionXmlDocument``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionXmlDocumentAttribute)

**Example of using RemoveRootNode. The result will be the same as the example above:**

```csharp
[PrefabExtension("ExampleFile", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle")]
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

[``PrefabExtensionInsertPatch``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch) also supports fetching and inserting xml from a file inside of your module's GUI folder.
The biggest advantage of doing this is being able to perform live debugging on your injected patch!

**Example of appending the content of a file using [``PrefabExtensionFileName``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch.PrefabExtensionFileNameAttribute):**

```csharp
[PrefabExtension("ExampleFile", "descendant::OptionsScreenWidget[@Id='Options']/Children/OptionsTabToggle")]
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
