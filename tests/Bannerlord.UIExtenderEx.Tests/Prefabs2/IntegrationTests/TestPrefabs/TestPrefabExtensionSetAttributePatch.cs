using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

using System.Collections.Generic;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2;

[PrefabExtension("SetAttribute2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children/OptionsTabToggle[@Id='SetAttribute']")]
internal class TestPrefabExtensionSetAttributePatch : PrefabExtensionSetAttributePatch
{
    public override List<Attribute> Attributes =>
    [
        new Attribute("CustomAttribute", "Value"),
        new Attribute("CustomAttribute2", "Value2")
    ];
}