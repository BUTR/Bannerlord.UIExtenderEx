using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2;

[PrefabExtension("ReplaceKeepChildren2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel")]
internal class TestPrefabExtensionInsertFileNamePatch : PrefabExtensionInsertPatch
{
    public override int Index => 3;

    public override InsertType Type => InsertType.ReplaceKeepChildren;

    [PrefabExtensionFileName]
    public virtual string GetPrefabExtension() => "Prefab";
}