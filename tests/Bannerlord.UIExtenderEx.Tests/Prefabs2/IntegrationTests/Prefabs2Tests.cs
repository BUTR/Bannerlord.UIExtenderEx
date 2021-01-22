using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
	public class Prefabs2Tests : BaseTests
	{
		private const int OptionsTabToggleCount = 6;
		private UIExtender? _uiExtender;

		public override void Setup()
		{
			base.Setup();

			_uiExtender = new UIExtender("TestModule2");
			_uiExtender.Register(typeof(PrefabsTests).Assembly);
			_uiExtender.Enable();
		}

		[OneTimeTearDown]
		public void Finalization()
		{
			_uiExtender?.Disable();
		}

		[Test]
		public void Prefabs2_Insert()
		{
			var widgetTemplateInsert = UIResourceManager.WidgetFactory.GetCustomType("Insert2").RootTemplate;
			List<WidgetTemplate>? optionsScreenWidget = GetChildren(widgetTemplateInsert);
			List<WidgetTemplate>? standardTopPanel = GetChildren(optionsScreenWidget[0]);
			List<WidgetTemplate>? listPanel = GetChildren(standardTopPanel[0]);
			Assert.AreEqual(OptionsTabToggleCount + 1, listPanel.Count, $"Children were: {string.Join(", ", listPanel.Select(x => $"Type: {x.Type} | Id: {x.Id}\n"))}");
			Assert.AreEqual("Insert", listPanel[3].Id);
		}

		[Test]
		public void Prefabs2_SetAttribute()
		{
			var widgetTemplateSetAttribute = UIResourceManager.WidgetFactory.GetCustomType("SetAttribute2").RootTemplate;
			List<WidgetTemplate>? optionsScreenWidget = GetChildren(widgetTemplateSetAttribute);
			List<WidgetTemplate>? standardTopPanel = GetChildren(optionsScreenWidget[0]);
			List<WidgetTemplate>? listPanel = GetChildren(standardTopPanel[0]);
			Assert.AreEqual(OptionsTabToggleCount, listPanel.Count, $"Children were: {string.Join(", ", listPanel.Select(x => $"Type: {x.Type} | Id: {x.Id}\n"))}");
			Assert.IsTrue(listPanel[3].AllAttributes.Any(a => a.Key == "CustomAttribute" && a.Value == "Value"));
			Assert.IsTrue(listPanel[3].AllAttributes.Any(a => a.Key == "CustomAttribute2" && a.Value == "Value2"));
		}

		[Test]
		public void Prefabs2_Append()
		{
			var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("Append2").RootTemplate;
			List<WidgetTemplate>? optionsScreenWidget = GetChildren(widgetTemplateAppend);
			List<WidgetTemplate>? standardTopPanel = GetChildren(optionsScreenWidget[0]);
			Assert.AreEqual(2, standardTopPanel.Count);
			Assert.AreEqual("Append", standardTopPanel[1].Id);
		}

		[Test]
		public void Prefabs2_Prepend()
		{
			var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("Prepend2").RootTemplate;
			List<WidgetTemplate>? optionsScreenWidget = GetChildren(widgetTemplateAppend);
			List<WidgetTemplate>? standardTopPanel = GetChildren(optionsScreenWidget[0]);
			Assert.AreEqual(2, standardTopPanel.Count);
			Assert.AreEqual("Prepend", standardTopPanel[0].Id);
		}

		[Test]
		public void Prefabs2_ReplaceKeepChildren()
		{
			var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildren2").RootTemplate;
			List<WidgetTemplate>? optionsScreenWidget = GetChildren(widgetTemplateAppend);
			List<WidgetTemplate>? standardTopPanel = GetChildren(optionsScreenWidget[0]);
			List<WidgetTemplate>? listPanel = GetChildren(standardTopPanel[0]);
			Assert.AreEqual(1, standardTopPanel.Count);
			Assert.AreEqual("ReplaceKeepChildren", standardTopPanel[0].Id);
			Assert.AreEqual(OptionsTabToggleCount, listPanel.Count, "ReplaceKeepChildren did not keep the original child count. " +
																	$"Remaining Children: {string.Join(", ", listPanel.Select(x => $"Type: {x.Type} | Id: {x.Id}\n"))}");
		}

		[Test]
		public void Prefabs2_AppendRemoveRootNode()
		{
			var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("AppendRemoveRootNode").RootTemplate;
			List<WidgetTemplate>? optionsScreenWidget = GetChildren(widgetTemplateAppend);
			List<WidgetTemplate>? standardTopPanel = GetChildren(optionsScreenWidget[0]);
			Assert.AreEqual(3, standardTopPanel.Count);
			Assert.AreEqual("Append1", standardTopPanel[1].Id);
			Assert.AreEqual("Append2", standardTopPanel[2].Id);
		}

		[Test]
		public void Prefabs2_PrependRemoveRootNode()
		{
			var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("PrependRemoveRootNode").RootTemplate;
			List<WidgetTemplate>? optionsScreenWidget = GetChildren(widgetTemplateAppend);
			List<WidgetTemplate>? standardTopPanel = GetChildren(optionsScreenWidget[0]);
			Assert.AreEqual(3, standardTopPanel.Count);
			Assert.AreEqual("Prepend1", standardTopPanel[0].Id);
			Assert.AreEqual("Prepend2", standardTopPanel[1].Id);
		}

		[Test]
		public void Prefabs2_ReplaceKeepChildrenRemoveRootNode()
		{
			var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildrenRemoveRootNode").RootTemplate;
			List<WidgetTemplate>? optionsScreenWidget = GetChildren(widgetTemplateAppend);
			List<WidgetTemplate>? standardTopPanel = GetChildren(optionsScreenWidget[0]);
			List<WidgetTemplate>? customListPanel3 = GetChildren(standardTopPanel[2]);
			Assert.AreEqual(4, standardTopPanel.Count);
			Assert.AreEqual("ReplaceKeepChildren1", standardTopPanel[0].Id);
			Assert.AreEqual("ReplaceKeepChildren2", standardTopPanel[1].Id);
			Assert.AreEqual("ReplaceKeepChildren3", standardTopPanel[2].Id);
			Assert.AreEqual("ReplaceKeepChildren4", standardTopPanel[3].Id);
			Assert.AreEqual(OptionsTabToggleCount, customListPanel3.Count, "ReplaceKeepChildren did not insert the children into correct new node. " +
																		   $"Should have been {standardTopPanel[2].Id}. Was: {standardTopPanel.FirstOrDefault(x => GetChildren(x).Count > 0)?.Id ?? "None"}");
		}
	}
}