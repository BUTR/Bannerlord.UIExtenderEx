using NUnit.Framework;

using TaleWorlds.Engine.GauntletUI;

namespace Bannerlord.UIExtenderEx.Tests
{
    public class PrefabsTests : BaseTests
    {
        [Test]
        public void PrefabsInsertTest()
        {
            var uiExtender = new UIExtender("TestModule");
            uiExtender.Register(typeof(PrefabsTests).Assembly);
            uiExtender.Enable();

            var widgetTemplateInsert = UIResourceManager.WidgetFactory.GetCustomType("Insert").RootTemplate;
            var childrenInsert1 = GetChildren(widgetTemplateInsert);
            var childrenInsert2 = GetChildren(childrenInsert1[0]);
            var childrenInsert3 = GetChildren(childrenInsert2[0]);
            Assert.True(childrenInsert3.Count == 5);
            Assert.True(childrenInsert3[4].Id == "Insert");

            var widgetTemplateReplace = UIResourceManager.WidgetFactory.GetCustomType("Replace").RootTemplate;
            var childrenReplace1 = GetChildren(widgetTemplateReplace);
            var childrenReplace2 = GetChildren(childrenReplace1[0]);
            var childrenReplace3 = GetChildren(childrenReplace2[0]);
            Assert.True(childrenReplace3.Count == 4);
            Assert.True(childrenReplace3[1].Id == "Replaced");

            var widgetTemplateInsertAsSiblingAppend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingAppend").RootTemplate;
            var childrenInsertAsSiblingAppend1 = GetChildren(widgetTemplateInsertAsSiblingAppend);
            var childrenInsertAsSiblingAppend2 = GetChildren(childrenInsertAsSiblingAppend1[0]);
            var childrenInsertAsSiblingAppend3 = GetChildren(childrenInsertAsSiblingAppend2[0]);
            Assert.True(childrenInsertAsSiblingAppend3.Count == 5);
            Assert.True(childrenInsertAsSiblingAppend3[1].Id == "InsertAsSiblingAppend");

            var widgetTemplateInsertAsSiblingPrepend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingPrepend").RootTemplate;
            var childrenInsertAsSiblingPrepend1 = GetChildren(widgetTemplateInsertAsSiblingPrepend);
            var childrenInsertAsSiblingPrepend2 = GetChildren(childrenInsertAsSiblingPrepend1[0]);
            var childrenInsertAsSiblingPrepend3 = GetChildren(childrenInsertAsSiblingPrepend2[0]);
            Assert.True(childrenInsertAsSiblingPrepend3.Count == 5);
            Assert.True(childrenInsertAsSiblingPrepend3[0].Id == "InsertAsSiblingPrepend");
        }
    }
}