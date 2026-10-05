namespace Bannerlord.UIExtenderEx.Tests.Generated
{
	public class GeneratedUIPrefabCreator
	{
		public global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult CreateDerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM(global::TaleWorlds.GauntletUI.UIContext context, global::System.Collections.Generic.Dictionary<string, object> data)
		{
			var widget = new DerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM(context);
			widget.CreateWidgets();
			widget.SetIds();
			widget.SetAttributes();
			var result = new global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult(widget);
			var movie = new global::TaleWorlds.GauntletUI.Data.GeneratedGauntletMovie("DerivedItemMovie", widget);
			var dataSource = data["DataSource"];
			widget.SetDataSource((global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.DerivedItemListVM)dataSource);
			result.AddData("Movie", movie);
			return result;
		}
		
		public void CollectGeneratedPrefabDefinitions(global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext generatedPrefabContext)
		{
			generatedPrefabContext.AddGeneratedPrefab("DerivedItemMovie", "Bannerlord.UIExtenderEx.Tests.CodeGenerator.DerivedItemListVM", CreateDerivedItemMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_DerivedItemListVM);
		}
		
	}
	
}

