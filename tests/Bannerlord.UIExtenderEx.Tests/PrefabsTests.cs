using HarmonyLib;

using NUnit.Framework;

using System.Collections.Generic;
using System.Xml;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests
{
    public class PrefabsTests
    {
        private class MockWidgetFactory : WidgetFactory
        {
            public static WidgetPrefab WidgetPrefabInsert { get; private set; } = default!;
            public static WidgetPrefab WidgetPrefabReplace { get; private set; } = default!;
            public static WidgetPrefab WidgetPrefabInsertAsSiblingAppend { get; private set; } = default!;
            public static WidgetPrefab WidgetPrefabInsertAsSiblingPrepend { get; private set; } = default!;

            private Dictionary<string, object> _customTypes = new Dictionary<string, object>
            {
                { "Insert", null! },
                { "Replace", null! },
                { "InsertAsSiblingAppend", null! },
                { "InsertAsSiblingPrepend", null! },
            };

            public MockWidgetFactory() : base(new MockResourceDepot(), string.Empty)
            {
                var harmony = new Harmony($"{nameof(MockWidgetFactory)}.ctor");
                harmony.Patch(AccessTools.Method(typeof(WidgetFactory), "GetPrefabNamesAndPathsFromCurrentPath"),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(MockWidgetFactory),
                        nameof(GetPrefabNamesAndPathsFromCurrentPathPrefix))));
                harmony.Patch(AccessTools.Method(typeof(WidgetFactory), "AddCustomType"),
                    prefix: new HarmonyMethod(
                        AccessTools.Method(typeof(MockWidgetFactory), nameof(AddCustomTypePrefix))));
                harmony.Patch(SymbolExtensions.GetMethodInfo(() => XmlReader.Create("", null!)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(MockWidgetFactory), nameof(CreatePrefix))));
            }

            public static bool CreatePrefix(ref XmlReader __result)
            {
                __result = XmlReader.Create(new System.IO.StringReader(@"
<Prefab>
  <Window>
    <OptionsScreenWidget Id=""Options"">
      <Children>
        <Standard.TopPanel Parameter.Title=""@OptionsLbl"">
          <Children>
            <ListPanel>
              <Children>
                <OptionsTabToggle Id=""InsertAsSibling""/>
                <OptionsTabToggle Id=""Replace""/>
                <OptionsTabToggle/>
                <OptionsTabToggle/>
              </Children>
            </ListPanel>
          </Children>
        </Standard.TopPanel>
      </Children>
    </OptionsScreenWidget>
  </Window>
</Prefab>
"), new XmlReaderSettings {IgnoreComments = true});
                return false;
            }

            public static bool AddCustomTypePrefix(string name, string path)
            {
                switch (name)
                {
                    case "Insert":
                        WidgetPrefabInsert = WidgetPrefab.LoadFrom(new PrefabExtensionContext(), new WidgetAttributeContext(), path);
                        break;

                    case "Replace":
                        WidgetPrefabReplace = WidgetPrefab.LoadFrom(new PrefabExtensionContext(), new WidgetAttributeContext(), path);
                        break;

                    case "InsertAsSiblingAppend":
                        WidgetPrefabInsertAsSiblingAppend = WidgetPrefab.LoadFrom(new PrefabExtensionContext(), new WidgetAttributeContext(), path);
                        break;

                    case "InsertAsSiblingPrepend ":
                        WidgetPrefabInsertAsSiblingPrepend = WidgetPrefab.LoadFrom(new PrefabExtensionContext(), new WidgetAttributeContext(), path);
                        break;
                }
                return false;
            }

            private static bool GetPrefabNamesAndPathsFromCurrentPathPrefix(ref Dictionary<string, string> __result)
            {
                __result = new Dictionary<string, string>
                {
                    { "Insert", "Insert.xml" },
                    { "Replace", "Replace.xml" },
                    { "InsertAsSiblingAppend", "InsertAsSiblingAppend.xml" },
                    { "InsertAsSiblingPrepend", "InsertAsSiblingPrepend.xml" },
                };
                return false;
            }
        }

        private class MockResourceDepot : ResourceDepot
        {
            public MockResourceDepot() : base(string.Empty) { }
        }

        private AccessTools.FieldRef<WidgetTemplate, List<WidgetTemplate>> GetChildren { get; } =
            AccessTools.FieldRefAccess<WidgetTemplate, List<WidgetTemplate>>("_children");

        [SetUp]
        public void Setup()
        {
            var property = AccessTools.Property(typeof(UIResourceManager), nameof(UIResourceManager.WidgetFactory));
            property.SetValue(null, new MockWidgetFactory());
        }

        [Test]
        public void PrefabsInsertTest()
        {
            var uiExtender = new UIExtender("TestModule");
            uiExtender.Register();
            uiExtender.Enable();

            var widgetTemplateInsert = MockWidgetFactory.WidgetPrefabInsert.RootTemplate;
            var childrenInsert1 = GetChildren(widgetTemplateInsert);
            var childrenInsert2 = GetChildren(childrenInsert1[0]);
            var childrenInsert3 = GetChildren(childrenInsert2[0]);
            Assert.True(childrenInsert3.Count == 5);
            Assert.True(childrenInsert3[4].Id == "Insert");

            var widgetTemplateReplace = MockWidgetFactory.WidgetPrefabReplace.RootTemplate;
            var childrenReplace1 = GetChildren(widgetTemplateReplace);
            var childrenReplace2 = GetChildren(childrenReplace1[0]);
            var childrenReplace3 = GetChildren(childrenReplace2[0]);
            Assert.True(childrenReplace3.Count == 4);
            Assert.True(childrenReplace3[1].Id == "Replaced");

            var widgetTemplateInsertAsSiblingAppend = MockWidgetFactory.WidgetPrefabInsertAsSiblingAppend.RootTemplate;
            var childrenInsertAsSiblingAppend1 = GetChildren(widgetTemplateInsertAsSiblingAppend);
            var childrenInsertAsSiblingAppend2 = GetChildren(childrenInsertAsSiblingAppend1[0]);
            var childrenInsertAsSiblingAppend3 = GetChildren(childrenInsertAsSiblingAppend2[0]);
            Assert.True(childrenInsertAsSiblingAppend3.Count == 5);
            Assert.True(childrenInsertAsSiblingAppend3[1].Id == "InsertAsSiblingAppend");

            var widgetTemplateInsertAsSiblingPrepend = MockWidgetFactory.WidgetPrefabInsertAsSiblingPrepend.RootTemplate;
            var childrenInsertAsSiblingPrepend1 = GetChildren(widgetTemplateInsertAsSiblingPrepend);
            var childrenInsertAsSiblingPrepend2 = GetChildren(childrenInsertAsSiblingPrepend1[0]);
            var childrenInsertAsSiblingPrepend3 = GetChildren(childrenInsertAsSiblingPrepend2[0]);
            Assert.True(childrenInsertAsSiblingPrepend3.Count == 5);
            Assert.True(childrenInsertAsSiblingPrepend3[0].Id == "InsertAsSiblingPrepend");
        }
    }
}