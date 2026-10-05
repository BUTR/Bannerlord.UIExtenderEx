namespace Bannerlord.UIExtenderEx.Tests.Generated
{
	public class GeneratedUIPrefabCreator
	{
		public global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult CreateFastPathEmissionMovie__Bannerlord_UIExtenderEx_Tests_CompiledPrefabs_FastPathRootVM(global::TaleWorlds.GauntletUI.UIContext context, global::System.Collections.Generic.Dictionary<string, object> data)
		{
			var widget = new FastPathEmissionMovie__Bannerlord_UIExtenderEx_Tests_CompiledPrefabs_FastPathRootVM(context);
			widget.CreateWidgets();
			widget.SetIds();
			widget.SetAttributes();
			var result = new global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult(widget);
			var movie = new global::TaleWorlds.GauntletUI.Data.GeneratedGauntletMovie("FastPathEmissionMovie", widget);
			var dataSource = data["DataSource"];
			widget.SetDataSource((global::Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.FastPathRootVM)dataSource);
			result.AddData("Movie", movie);
			return result;
		}
		
		public void CollectGeneratedPrefabDefinitions(global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext generatedPrefabContext)
		{
			generatedPrefabContext.AddGeneratedPrefab("FastPathEmissionMovie", "Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.FastPathRootVM", CreateFastPathEmissionMovie__Bannerlord_UIExtenderEx_Tests_CompiledPrefabs_FastPathRootVM);
		}
		
	}
	
}

