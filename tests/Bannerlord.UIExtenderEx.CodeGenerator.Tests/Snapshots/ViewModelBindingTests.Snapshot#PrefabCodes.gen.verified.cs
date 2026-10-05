namespace Bannerlord.UIExtenderEx.Tests.Generated
{
	public class GeneratedUIPrefabCreator
	{
		public global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult CreateBindingMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_NonPublicMemberVM(global::TaleWorlds.GauntletUI.UIContext context, global::System.Collections.Generic.Dictionary<string, object> data)
		{
			var widget = new BindingMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_NonPublicMemberVM(context);
			widget.CreateWidgets();
			widget.SetIds();
			widget.SetAttributes();
			var result = new global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabInstantiationResult(widget);
			var movie = new global::TaleWorlds.GauntletUI.Data.GeneratedGauntletMovie("BindingMovie", widget);
			var dataSource = data["DataSource"];
			widget.SetDataSource((global::Bannerlord.UIExtenderEx.Tests.CodeGenerator.NonPublicMemberVM)dataSource);
			result.AddData("Movie", movie);
			return result;
		}
		
		public void CollectGeneratedPrefabDefinitions(global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext generatedPrefabContext)
		{
			generatedPrefabContext.AddGeneratedPrefab("BindingMovie", "Bannerlord.UIExtenderEx.Tests.CodeGenerator.NonPublicMemberVM", CreateBindingMovie__Bannerlord_UIExtenderEx_Tests_CodeGenerator_NonPublicMemberVM);
		}
		
	}
	
}

