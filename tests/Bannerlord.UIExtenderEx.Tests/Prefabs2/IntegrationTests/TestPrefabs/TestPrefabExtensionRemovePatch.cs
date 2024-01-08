using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2;

[PrefabExtension("Remove", "descendant::OptionsScreenWidget[@Id='Options']")]
internal class TestPrefabExtensionRemovePatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Remove;
}