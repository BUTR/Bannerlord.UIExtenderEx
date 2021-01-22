using System.Xml;

using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
	[PrefabExtension("ReplaceKeepChildren2", "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel")]
	internal class TestPrefabExtensionInsertTextPatch : PrefabExtensionInsertPatch
	{
		public override int Index => 3;

		public override InsertType Type => InsertType.ReplaceKeepChildren;

		[PrefabExtensionText]
		public virtual string GetPrefabExtension() => "<CustomListPanel Id=\"ReplaceKeepChildren\" />";
	}
}