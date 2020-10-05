using HarmonyLib;

using NUnit.Framework;

using System.Collections.Generic;
using System.Xml;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests
{
    public class BaseTests
    {
        protected class MockWidgetFactory : WidgetFactory
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
                harmony.Patch(AccessTools.DeclaredMethod(typeof(WidgetFactory), "GetPrefabNamesAndPathsFromCurrentPath"),
                    prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(MockWidgetFactory),
                        nameof(GetPrefabNamesAndPathsFromCurrentPathPrefix))));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(WidgetFactory), "AddCustomType"),
                    prefix: new HarmonyMethod(
                        AccessTools.DeclaredMethod(typeof(MockWidgetFactory), nameof(AddCustomTypePrefix))));
                harmony.Patch(SymbolExtensions.GetMethodInfo(() => XmlReader.Create("", null!)),
                    prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(MockWidgetFactory), nameof(CreatePrefix))));
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
"), new XmlReaderSettings { IgnoreComments = true });
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

                    case "InsertAsSiblingPrepend":
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

        protected class MockResourceDepot : ResourceDepot
        {
            public MockResourceDepot() : base(string.Empty) { }
        }

        protected AccessTools.FieldRef<WidgetTemplate, List<WidgetTemplate>> GetChildren { get; } =
            AccessTools.FieldRefAccess<WidgetTemplate, List<WidgetTemplate>>("_children");

        [SetUp]
        public void Setup()
        {
            var property = AccessTools.DeclaredProperty(typeof(UIResourceManager), nameof(UIResourceManager.WidgetFactory));
            property.SetValue(null, new MockWidgetFactory());
        }
    }
}