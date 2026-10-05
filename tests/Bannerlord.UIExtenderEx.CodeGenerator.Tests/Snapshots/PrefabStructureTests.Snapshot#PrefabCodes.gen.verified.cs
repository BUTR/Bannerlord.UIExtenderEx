namespace Bannerlord.UIExtenderEx.Tests.Generated
{
	public class GeneratedUIPrefabCreator
	{
		public global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult CreateStructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM(global::TaleWorlds.GauntletUI.UIContext context, global::System.Collections.Generic.Dictionary<string, object> data)
		{
			var widget = new StructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM(context);
			widget.CreateWidgets();
			widget.SetIds();
			widget.SetAttributes();
			var result = new global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult(widget);
			var movie = new global::TaleWorlds.GauntletUI.Data.GeneratedGauntletMovie("StructureMovie", widget);
			var dataSource = data["DataSource"];
			widget.SetDataSource((global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM)dataSource);
			result.AddData("Movie", movie);
			return result;
		}
		
		public void CollectGeneratedPrefabDefinitions(global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext generatedPrefabContext)
		{
			generatedPrefabContext.AddGeneratedPrefab("StructureMovie", "Bannerlord.UIExtenderEx.Tests.CodeGenerator.StructureVM", CreateStructureMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_StructureVM);
		}
		
	}
	
}

