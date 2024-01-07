using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2;

[PrefabExtension("ReplaceKeepChildrenRemoveRootNode", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel")]
internal class TestPrefabExtensionInsertFileNamePatchRemoveRootNode : PrefabExtensionInsertPatch
{
    public override int Index => 2;

    public override InsertType Type => InsertType.ReplaceKeepChildren;

    [PrefabExtensionFileName(true)]
    public virtual string GetPrefabExtension() => "Prefab";
}