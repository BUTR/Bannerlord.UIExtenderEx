using System.Collections;
using System.Collections.Generic;
using System.Xml;

using HarmonyLib;

using NUnit.Framework;

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

using StringReader = System.IO.StringReader;

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
                    new HarmonyMethod(AccessTools.DeclaredMethod(typeof(MockWidgetFactory),
                        nameof(GetPrefabNamesAndPathsFromCurrentPathPrefix))));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(WidgetFactory), nameof(GetCustomType)),
                    new HarmonyMethod(
                        AccessTools.DeclaredMethod(typeof(MockWidgetFactory), nameof(GetCustomTypePrefix))));
                harmony.Patch(SymbolExtensions.GetMethodInfo(() => XmlReader.Create("", null!)),
                    new HarmonyMethod(AccessTools.DeclaredMethod(typeof(MockWidgetFactory), nameof(CreatePrefix))));

                if (GetCustomTypes?.Invoke(this) is { } dictionary)
                {
                    dictionary.Add("SetAttribute", null!);
                    dictionary.Add("Insert", null!);
                    dictionary.Add("ReplaceKeepChildren", null!);
                    dictionary.Add("InsertAsSiblingAppend", null!);
                    dictionary.Add("InsertAsSiblingPrepend", null!);

                    dictionary.Add("SetAttribute2", null!);
                    dictionary.Add("Insert2", null!);
                    dictionary.Add("ReplaceKeepChildren2", null!);
                    dictionary.Add("Append2", null!);
                    dictionary.Add("Prepend2", null!);
                    dictionary.Add("ReplaceKeepChildrenRemoveRootNode", null!);
                    dictionary.Add("AppendRemoveRootNode", null!);
                    dictionary.Add("PrependRemoveRootNode", null!);
                }
            }

            public static bool CreatePrefix(ref XmlReader __result)
            {
                __result = XmlReader.Create(new StringReader(@"
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
                <OptionsTabToggle Id=""ReplaceKeepChildren""/>
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
"), new XmlReaderSettings {IgnoreComments = true});
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
                    {"SetAttribute", "SetAttribute.xml"},
                    {"Insert", "Insert.xml"},
                    {"ReplaceKeepChildren", "ReplaceKeepChildren.xml"},
                    {"InsertAsSiblingAppend", "InsertAsSiblingAppend.xml"},
                    {"InsertAsSiblingPrepend", "InsertAsSiblingPrepend.xml"},

                    {"SetAttribute2", "SetAttribute2.xml"},
                    {"Insert2", "Insert2.xml"},
                    {"ReplaceKeepChildren2", "ReplaceKeepChildren2.xml"},
                    {"Append2", "Append2.xml"},
                    {"Prepend2", "Prepend2.xml"},
                    {"ReplaceKeepChildrenRemoveRootNode", "ReplaceKeepChildrenRemoveRootNode.xml"},
                    {"AppendRemoveRootNode", "AppendRemoveRootNode.xml"},
                    {"PrependRemoveRootNode", "PrependRemoveRootNode.xml"}
                };
                return false;
            }
        }

        protected AccessTools.FieldRef<WidgetTemplate, List<WidgetTemplate>> GetChildren { get; } =
            AccessTools.FieldRefAccess<WidgetTemplate, List<WidgetTemplate>>("_children");

        [OneTimeSetUp]
        public virtual void Setup()
        {
            var property = AccessTools.DeclaredProperty(typeof(UIResourceManager), nameof(UIResourceManager.WidgetFactory));
            property.SetValue(null, new MockWidgetFactory());
        }
    }
}