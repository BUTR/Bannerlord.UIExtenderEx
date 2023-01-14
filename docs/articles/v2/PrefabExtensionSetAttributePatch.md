### [``PrefabExtensionSetAttributePatch``](xref:Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch)  
Patch that adds or replaces a node's attributes. The target node should be specified by the XPath in the [``PrefabExtension``](xref:Bannerlord.UIExtenderEx.Attributes.PrefabExtensionAttribute)
If the attribute already exists on the target node, it's value will be replaced by the specified value. Otherwise, the new attribute is added with the specified value.

---

**Example of adding multiple attributes:**

```csharp
[PrefabExtension( "ExampleFile", "descendant::OptionScreenWidget[@Id='Options']/Children/OptionsTabToggle" )]
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
