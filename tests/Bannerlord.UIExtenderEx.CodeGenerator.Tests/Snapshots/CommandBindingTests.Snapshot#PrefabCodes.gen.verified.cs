namespace Bannerlord.UIExtenderEx.Tests.Generated
{
	public class GeneratedUIPrefabCreator
	{
		public global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult CreateCommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM(global::TaleWorlds.GauntletUI.UIContext context, global::System.Collections.Generic.Dictionary<string, object> data)
		{
			var widget = new CommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM(context);
			widget.CreateWidgets();
			widget.SetIds();
			widget.SetAttributes();
			var result = new global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult(widget);
			var movie = new global::TaleWorlds.GauntletUI.Data.GeneratedGauntletMovie("CommandMovie", widget);
			var dataSource = data["DataSource"];
			widget.SetDataSource((global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM)dataSource);
			result.AddData("Movie", movie);
			return result;
		}
		
		public void CollectGeneratedPrefabDefinitions(global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext generatedPrefabContext)
		{
			generatedPrefabContext.AddGeneratedPrefab("CommandMovie", "Bannerlord.UIExtenderEx.Tests.CodeGenerator.CommandVM", CreateCommandMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_CommandVM);
		}
		
	}
	
}

