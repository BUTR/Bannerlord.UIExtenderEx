using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests
{
    public class PrefabsTests : BaseTests
    {
        private const int Elements = 6;

        [Test]
        public void PrefabsInsertTest()
        {
            var uiExtender = new UIExtender("TestModule");
            uiExtender.Register(typeof(PrefabsTests).Assembly);
            uiExtender.Enable();

            var widgetTemplateInsert = UIResourceManager.WidgetFactory.GetCustomType("Insert").RootTemplate;
            List<WidgetTemplate>? childrenInsert1 = GetChildren(widgetTemplateInsert);
            List<WidgetTemplate>? childrenInsert2 = GetChildren(childrenInsert1[0]);
            List<WidgetTemplate>? childrenInsert3 = GetChildren(childrenInsert2[0]);
            Assert.True(childrenInsert3.Count == Elements + 1);
            Assert.True(childrenInsert3[4].Id == "Insert");

            var widgetTemplateReplace = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildren").RootTemplate;
            List<WidgetTemplate>? childrenReplace1 = GetChildren(widgetTemplateReplace);
            List<WidgetTemplate>? childrenReplace2 = GetChildren(childrenReplace1[0]);
            List<WidgetTemplate>? childrenReplace3 = GetChildren(childrenReplace2[0]);
            Assert.True(childrenReplace3.Count == Elements);
            Assert.True(childrenReplace3[2].Id == "Replaced");

            var widgetTemplateInsertAsSiblingAppend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingAppend").RootTemplate;
            List<WidgetTemplate>? childrenInsertAsSiblingAppend1 = GetChildren(widgetTemplateInsertAsSiblingAppend);
            List<WidgetTemplate>? childrenInsertAsSiblingAppend2 = GetChildren(childrenInsertAsSiblingAppend1[0]);
            List<WidgetTemplate>? childrenInsertAsSiblingAppend3 = GetChildren(childrenInsertAsSiblingAppend2[0]);
            Assert.True(childrenInsertAsSiblingAppend3.Count == Elements + 1);
            Assert.True(childrenInsertAsSiblingAppend3[1].Id == "InsertAsSiblingAppend");

            var widgetTemplateInsertAsSiblingPrepend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingPrepend").RootTemplate;
            List<WidgetTemplate>? childrenInsertAsSiblingPrepend1 = GetChildren(widgetTemplateInsertAsSiblingPrepend);
            List<WidgetTemplate>? childrenInsertAsSiblingPrepend2 = GetChildren(childrenInsertAsSiblingPrepend1[0]);
            List<WidgetTemplate>? childrenInsertAsSiblingPrepend3 = GetChildren(childrenInsertAsSiblingPrepend2[0]);
            Assert.True(childrenInsertAsSiblingPrepend3.Count == Elements + 1);
            Assert.True(childrenInsertAsSiblingPrepend3[0].Id == "InsertAsSiblingPrepend");

            var widgetTemplateSetAttribute = UIResourceManager.WidgetFactory.GetCustomType("SetAttribute").RootTemplate;
            List<WidgetTemplate>? childrenSetAttribute1 = GetChildren(widgetTemplateSetAttribute);
            List<WidgetTemplate>? childrenSetAttribute2 = GetChildren(childrenSetAttribute1[0]);
            List<WidgetTemplate>? childrenSetAttribute3 = GetChildren(childrenSetAttribute2[0]);
            Assert.True(childrenSetAttribute3.Count == Elements);
            Assert.True(childrenSetAttribute3[3].AllAttributes.Any(a => a.Key == "CustomAttribute" && a.Value == "Value"));

            uiExtender.Disable();
        }
    }
}