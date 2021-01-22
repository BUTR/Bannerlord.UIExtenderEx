using System.Xml;

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
    [PrefabExtension("ReplaceKeepChildrenRemoveRootNode", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel")]
    internal class TestPrefabExtensionInsertTextPatchRemoveRootNode : PrefabExtensionInsertPatch
    {
        public override int Index => 2;

        public override InsertType Type => InsertType.ReplaceKeepChildren;

        [PrefabExtensionText(true)]
        public virtual string GetPrefabExtension() => "<DiscardedRoot>" +
                                                      "<CustomListPanel Id=\"ReplaceKeepChildren1\"/>" +
                                                      "<CustomListPanel Id=\"ReplaceKeepChildren2\"/>" +
                                                      "<CustomListPanel Id=\"ReplaceKeepChildren3\"/>" +
                                                      "<CustomListPanel Id=\"ReplaceKeepChildren4\"/>" +
                                                      "</DiscardedRoot>";
    }
}