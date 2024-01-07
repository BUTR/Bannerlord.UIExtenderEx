using NUnit.Framework;

using System.Linq;

using TaleWorlds.Engine.GauntletUI;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2;

public class Prefabs2Tests : BaseTests
{
    private const int OptionsTabToggleCount = 6;
    private UIExtender? _uiExtender;

    [SetUp]
    public void Setup()
    {
        _uiExtender = UIExtender.Create("TestModule");
        _uiExtender.Register(new[]
        {
            typeof(TestPrefabExtensionInsertFileNamePatch),
            typeof(TestPrefabExtensionInsertFileNamePatchRemoveRootNode),
            typeof(TestPrefabExtensionInsertTextPatch),
            typeof(TestPrefabExtensionInsertTextPatchRemoveRootNode),
            typeof(TestPrefabExtensionInsertXmlDocumentPatch),
            typeof(TestPrefabExtensionInsertXmlDocumentPatchRemoveRootNode),
            typeof(TestPrefabExtensionInsertXmlNodePatch),
            typeof(TestPrefabExtensionInsertXmlNodePatchRemoveRootNode),
            typeof(TestPrefabExtensionInsertXmlNodesPatch),
            typeof(TestPrefabExtensionRemovePatch),
            typeof(TestPrefabExtensionSetAttributePatch),
        });
        _uiExtender.Enable();
    }

    [TearDown]
    public void Finalization()
    {
        _uiExtender?.Deregister();
    }

    [Test]
    public void Prefabs2_Insert()
    {
        var widgetTemplateInsert = UIResourceManager.WidgetFactory.GetCustomType("Insert2").RootTemplate;
        var optionsScreenWidget = GetChildren(widgetTemplateInsert);
        var standardTopPanel = GetChildren(optionsScreenWidget[0]);
        var listPanel = GetChildren(standardTopPanel[0]);
        Assert.AreEqual(OptionsTabToggleCount + 1, listPanel.Count, $"Children were: {string.Join(", ", listPanel.Select(x => $"Type: {x.Type} | Id: {x.Id}\n"))}");
        Assert.AreEqual("Insert", listPanel[3].Id);
    }

    [Test]
    public void Prefabs2_SetAttribute()
    {
        var widgetTemplateSetAttribute = UIResourceManager.WidgetFactory.GetCustomType("SetAttribute2").RootTemplate;
        var optionsScreenWidget = GetChildren(widgetTemplateSetAttribute);
        var standardTopPanel = GetChildren(optionsScreenWidget[0]);
        var listPanel = GetChildren(standardTopPanel[0]);
        Assert.AreEqual(OptionsTabToggleCount, listPanel.Count, $"Children were: {string.Join(", ", listPanel.Select(x => $"Type: {x.Type} | Id: {x.Id}\n"))}");
        Assert.IsTrue(listPanel[3].AllAttributes.Any(a => a.Key == "CustomAttribute" && a.Value == "Value"));
        Assert.IsTrue(listPanel[3].AllAttributes.Any(a => a.Key == "CustomAttribute2" && a.Value == "Value2"));
    }

    [Test]
    public void Prefabs2_Append()
    {
        var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("Append2").RootTemplate;
        var optionsScreenWidget = GetChildren(widgetTemplateAppend);
        var standardTopPanel = GetChildren(optionsScreenWidget[0]);
        Assert.AreEqual(2, standardTopPanel.Count);
        Assert.AreEqual("Append", standardTopPanel[1].Id);
    }

    [Test]
    public void Prefabs2_Prepend()
    {
        var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("Prepend2").RootTemplate;
        var optionsScreenWidget = GetChildren(widgetTemplateAppend);
        var standardTopPanel = GetChildren(optionsScreenWidget[0]);
        Assert.AreEqual(2, standardTopPanel.Count);
        Assert.AreEqual("Prepend", standardTopPanel[0].Id);
    }

    /*
    [Test]
    public void Prefabs2_ReplaceKeepChildren()
    {
        var optionsScreenWidget = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildren2").RootTemplate;
        var standardTopPanel = GetChildren(optionsScreenWidget);
        var validRoot = GetChildren(standardTopPanel[0]);
        var listPanel = GetChildren(validRoot[0]);
        Assert.AreEqual(1, standardTopPanel.Count);
        Assert.AreEqual("ReplaceKeepChildren", standardTopPanel[0].Id);
        Assert.AreEqual(OptionsTabToggleCount, listPanel.Count, "ReplaceKeepChildren did not keep the original child count. " +
                                                                $"Remaining Children: {string.Join(", ", listPanel.Select(x => $"Type: {x.Type} | Id: {x.Id}\n"))}");
    }
    */

    [Test]
    public void Prefabs2_AppendRemoveRootNode()
    {
        var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("AppendRemoveRootNode").RootTemplate;
        var optionsScreenWidget = GetChildren(widgetTemplateAppend);
        var standardTopPanel = GetChildren(optionsScreenWidget[0]);
        Assert.AreEqual(3, standardTopPanel.Count);
        Assert.AreEqual("Append1", standardTopPanel[1].Id);
        Assert.AreEqual("Append2", standardTopPanel[2].Id);
    }

    [Test]
    public void Prefabs2_PrependRemoveRootNode()
    {
        var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("PrependRemoveRootNode").RootTemplate;
        var optionsScreenWidget = GetChildren(widgetTemplateAppend);
        var standardTopPanel = GetChildren(optionsScreenWidget[0]);
        Assert.AreEqual(3, standardTopPanel.Count);
        Assert.AreEqual("Prepend1", standardTopPanel[0].Id);
        Assert.AreEqual("Prepend2", standardTopPanel[1].Id);
    }

    /*
    [Test]
    public void Prefabs2_ReplaceKeepChildrenRemoveRootNode()
    {
        var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildrenRemoveRootNode").RootTemplate;
        var optionsScreenWidget = GetChildren(widgetTemplateAppend);
        var standardTopPanel = GetChildren(optionsScreenWidget[0]);
        var customListPanel3 = GetChildren(standardTopPanel[2]);
        Assert.AreEqual(4, standardTopPanel.Count);
        Assert.AreEqual("ReplaceKeepChildren1", standardTopPanel[0].Id);
        Assert.AreEqual("ReplaceKeepChildren2", standardTopPanel[1].Id);
        Assert.AreEqual("ReplaceKeepChildren3", standardTopPanel[2].Id);
        Assert.AreEqual("ReplaceKeepChildren4", standardTopPanel[3].Id);
        Assert.AreEqual(OptionsTabToggleCount, customListPanel3.Count, "ReplaceKeepChildren did not insert the children into correct new node. " +
                                                                       $"Should have been {standardTopPanel[2].Id}. Was: {standardTopPanel.FirstOrDefault(x => GetChildren(x).Count > 0)?.Id ?? "None"}");
    }
    */
}