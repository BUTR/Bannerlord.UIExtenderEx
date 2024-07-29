using Bannerlord.UIExtenderEx.Tests.Utils;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

using TaleWorlds.GauntletUI.PrefabSystem;

using StringReader = System.IO.StringReader;

namespace Bannerlord.UIExtenderEx.Tests;

public class BaseTests : SharedTests
{
    protected class MockWidgetFactory : WidgetFactory
    {
        private static readonly AccessTools.FieldRef<object, IDictionary>? GetCustomTypes =
            AccessTools2.FieldRefAccess<IDictionary>("TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:_customTypes");

        public MockWidgetFactory() : base(ResourceDepotUtils.Create(), string.Empty)
        {
            var harmony = new Harmony($"{nameof(MockWidgetFactory)}.ctor");
            harmony.Patch(AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:GetPrefabNamesAndPathsFromCurrentPath"),
                new HarmonyMethod(typeof(MockWidgetFactory), nameof(GetPrefabNamesAndPathsFromCurrentPathPrefix)));
            harmony.Patch(AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:GetCustomType"),
                new HarmonyMethod(typeof(MockWidgetFactory), nameof(GetCustomTypePrefix)));
            harmony.Patch(AccessTools2.DeclaredMethod("System.Xml.XmlReader:Create", [typeof(string), typeof(XmlReaderSettings)]),
                new HarmonyMethod(typeof(MockWidgetFactory), nameof(CreatePrefix)));

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
                {"PrependRemoveRootNode", "PrependRemoveRootNode.xml"},
                {"Remove", "Remove.xml"},
            };
            return false;
        }
    }

    protected AccessTools.FieldRef<WidgetTemplate, List<WidgetTemplate>> GetChildren { get; } =
        AccessTools.FieldRefAccess<WidgetTemplate, List<WidgetTemplate>>("_children")!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _ = Assembly.Load("TaleWorlds.Engine.GauntletUI");

        var property = AccessTools2.DeclaredProperty("TaleWorlds.Engine.GauntletUI.UIResourceManager:WidgetFactory");
        property!.SetValue(null, new MockWidgetFactory());
    }
}