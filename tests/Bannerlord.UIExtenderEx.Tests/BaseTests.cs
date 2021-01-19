using HarmonyLib;

using NUnit.Framework;

using System.Collections;
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
            private static readonly AccessTools.FieldRef<object, IDictionary>? GetCustomTypes =
                AccessTools3.FieldRefAccess<IDictionary>(typeof(WidgetFactory), "_customTypes");

            public MockWidgetFactory() : base(new ResourceDepot(string.Empty), string.Empty)
            {
                var harmony = new Harmony($"{nameof(MockWidgetFactory)}.ctor");
                harmony.Patch(AccessTools.DeclaredMethod(typeof(WidgetFactory), "GetPrefabNamesAndPathsFromCurrentPath"),
                    prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(MockWidgetFactory),
                        nameof(GetPrefabNamesAndPathsFromCurrentPathPrefix))));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(WidgetFactory), nameof(WidgetFactory.GetCustomType)),
                    prefix: new HarmonyMethod(
                        AccessTools.DeclaredMethod(typeof(MockWidgetFactory), nameof(GetCustomTypePrefix))));
                harmony.Patch(SymbolExtensions.GetMethodInfo(() => XmlReader.Create("", null!)),
                    prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(MockWidgetFactory), nameof(CreatePrefix))));

                if (GetCustomTypes?.Invoke(this) is { } dictionary)
                {
                    dictionary.Add("SetAttribute", null!);
                    dictionary.Add("Insert", null!);
                    dictionary.Add("Replace", null!);
                    dictionary.Add("InsertAsSiblingAppend", null!);
                    dictionary.Add("InsertAsSiblingPrepend", null!);

                    dictionary.Add("SetAttribute2", null!);
                    dictionary.Add("Insert2", null!);
                    dictionary.Add("Replace2", null!);
                    dictionary.Add("InsertAsSiblingAppend2", null!);
                    dictionary.Add("InsertAsSiblingPrepend2", null!);
                }
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
                <OptionsTabToggle Id=""InsertAsSibling""/>
                <OptionsTabToggle Id=""Replace""/>
                <OptionsTabToggle Id=""SetAttribute""/>
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

            public static bool GetCustomTypePrefix(string typeName, ref WidgetPrefab __result)
            {
                __result = WidgetPrefab.LoadFrom(new PrefabExtensionContext(), new WidgetAttributeContext(), typeName);
                return false;
            }

            private static bool GetPrefabNamesAndPathsFromCurrentPathPrefix(ref Dictionary<string, string> __result)
            {
                __result = new Dictionary<string, string>
                {
                    { "SetAttribute", "SetAttribute.xml" },
                    { "Insert", "Insert.xml" },
                    { "Replace", "Replace.xml" },
                    { "InsertAsSiblingAppend", "InsertAsSiblingAppend.xml" },
                    { "InsertAsSiblingPrepend", "InsertAsSiblingPrepend.xml" },

                    { "SetAttribute2", "SetAttribute2.xml" },
                    { "Insert2", "Insert2.xml" },
                    { "Replace2", "Replace2.xml" },
                    { "InsertAsSiblingAppend2", "InsertAsSiblingAppend2.xml" },
                    { "InsertAsSiblingPrepend2", "InsertAsSiblingPrepend2.xml" },
                };
                return false;
            }
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