using Bannerlord.UIExtenderEx.Components;
using Bannerlord.UIExtenderEx.Prefabs;

using NUnit.Framework;

using System;
using System.Linq;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs;

#pragma warning disable CS0618 // Suppresses obsolete warning to validate legacy v1 insert patch behavior.
public class PrefabComponentPrefabsTests
{
    private const string MovieName = "TestMovieName";
    private const string ListChildrenXPath = "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel/Children/ListPanel/Children";
    private const string ReplaceKeepChildrenXPath = ListChildrenXPath + "/OptionsTabToggle[@Id='ReplaceKeepChildren']";
    private const string SetAttributeXPath = ListChildrenXPath + "/OptionsTabToggle[@Id='SetAttribute']";

    private static readonly string[] BaseChildren = ["InsertAsSibling", "InsertAsSibling", "ReplaceKeepChildren", "SetAttribute", "OptionsTabToggle", "OptionsTabToggle"];

    private sealed class TestInsertPatch : PrefabExtensionInsertPatch
    {
        private readonly string _xml;
        public TestInsertPatch(int position, string xml = "<Inserted/>")
        {
            Position = position;
            _xml = xml;
        }

        public override string Id => "Insert";
        public override int Position { get; }
        public override XmlDocument GetPrefabExtension() => Load(_xml);
    }

    private sealed class TestInsertAsSiblingPatch : PrefabExtensionInsertAsSiblingPatch
    {
        public TestInsertAsSiblingPatch(InsertType type) => Type = type;

        public override string Id => "InsertAsSibling";
        public override InsertType Type { get; }
        public override XmlDocument GetPrefabExtension() => Load("<Inserted/>");
    }

    private sealed class TestReplacePatch : PrefabExtensionReplacePatch
    {
        public override string Id => "Replace";
        public override XmlDocument GetPrefabExtension() => Load("<Replaced><SomeChild/></Replaced>");
    }

    private sealed class TestSetAttributePatch : PrefabExtensionSetAttributePatch
    {
        public TestSetAttributePatch(string attribute, string value)
        {
            Attribute = attribute;
            Value = value;
        }

        public override string Id => "SetAttribute";
        public override string Attribute { get; }
        public override string Value { get; }
    }

    private static XmlDocument Load(string xml)
    {
        XmlDocument document = new();
        document.LoadXml(xml);
        return document;
    }

    private static XmlDocument GetBaseDocument() => Load(@"
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
        <Child2/>
        <Child3/>
      </Children>
    </OptionsScreenWidget>
  </Window>
</Prefab>
");

    private static XmlDocument Apply(Action<PrefabComponent> register)
    {
        PrefabComponent prefabComponent = new("TestModule");
        var movieDocument = GetBaseDocument();

        register(prefabComponent);
        prefabComponent.Enable();
        prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);
        prefabComponent.Deregister();

        return movieDocument;
    }

    /// <summary>
    /// The children of the list the patches target, each named by its Id, or by its element name when it has none.
    /// </summary>
    private static string[] ListChildren(XmlDocument document) => document.SelectSingleNode(ListChildrenXPath)!.ChildNodes
        .Cast<XmlNode>()
        .Select(x => x.Attributes?["Id"]?.Value ?? x.Name)
        .ToArray();

    private static string[] WithInsertedAt(int index) => [.. BaseChildren.Take(index), "Inserted", .. BaseChildren.Skip(index)];

    // v1 inserts after the child at Position, so PositionFirst lands second - not what its doc says, but what mods have always got.
    [TestCase(TestInsertPatch.PositionFirst, 1)]
    [TestCase(2, 3)]
    [TestCase(5, 6)]
    [TestCase(TestInsertPatch.PositionLast, 6)]
    [TestCase(-5, 1)]
    public void Insert_PlacesAfterTheChildAtPosition(int position, int expectedIndex)
    {
        var document = Apply(x => x.RegisterPatch(MovieName, ListChildrenXPath, new TestInsertPatch(position)));

        CollectionAssert.AreEqual(WithInsertedAt(expectedIndex), ListChildren(document));
    }

    [Test]
    public void Insert_KeepsTheExtensionSubtreeWithoutComments()
    {
        var document = Apply(x => x.RegisterPatch(MovieName, ListChildrenXPath, new TestInsertPatch(0, "<Inserted><!-- note --><SomeChild/></Inserted>")));

        var inserted = document.SelectSingleNode("descendant::Inserted");
        Assert.IsNotNull(inserted);
        Assert.AreEqual(1, inserted!.ChildNodes.Count);
        Assert.AreEqual("SomeChild", inserted.FirstChild.Name);
    }

    [Test]
    public void Insert_IntoNodeWithoutChildren_AppendsTheChild()
    {
        var document = Apply(x => x.RegisterPatch(MovieName, "descendant::Child2", new TestInsertPatch(TestInsertPatch.PositionFirst)));

        var child2 = document.SelectSingleNode("descendant::Child2");
        Assert.AreEqual(1, child2!.ChildNodes.Count);
        Assert.AreEqual("Inserted", child2.FirstChild.Name);
    }

    [Test]
    public void InsertAsSibling_Append_PlacesAfterTheTarget()
    {
        var document = Apply(x => x.RegisterPatch(MovieName, ReplaceKeepChildrenXPath, new TestInsertAsSiblingPatch(PrefabExtensionInsertAsSiblingPatch.InsertType.Append)));

        CollectionAssert.AreEqual(WithInsertedAt(3), ListChildren(document));
    }

    [Test]
    public void InsertAsSibling_Prepend_PlacesBeforeTheTarget()
    {
        var document = Apply(x => x.RegisterPatch(MovieName, ReplaceKeepChildrenXPath, new TestInsertAsSiblingPatch(PrefabExtensionInsertAsSiblingPatch.InsertType.Prepend)));

        CollectionAssert.AreEqual(WithInsertedAt(2), ListChildren(document));
    }

    [Test]
    public void InsertAsSibling_UndefinedType_LeavesTheDocumentAlone()
    {
        var document = Apply(x => x.RegisterPatch(MovieName, ReplaceKeepChildrenXPath, new TestInsertAsSiblingPatch((PrefabExtensionInsertAsSiblingPatch.InsertType) 42)));

        CollectionAssert.AreEqual(BaseChildren, ListChildren(document));
    }

    [Test]
    public void Replace_SwapsTheTargetForTheExtension()
    {
        var document = Apply(x => x.RegisterPatch(MovieName, ReplaceKeepChildrenXPath, new TestReplacePatch()));

        string[] expected = [.. BaseChildren];
        expected[2] = "Replaced";
        CollectionAssert.AreEqual(expected, ListChildren(document));
        Assert.AreEqual("SomeChild", document.SelectSingleNode("descendant::Replaced")!.FirstChild.Name);
    }

    [Test]
    public void SetAttribute_AddsAMissingAttribute()
    {
        var document = Apply(x => x.RegisterPatch(MovieName, SetAttributeXPath, new TestSetAttributePatch("CustomAttribute", "Value")));

        Assert.AreEqual("Value", document.SelectSingleNode(SetAttributeXPath)!.Attributes!["CustomAttribute"]?.Value);
    }

    [Test]
    public void SetAttribute_OverwritesAnExistingAttribute()
    {
        var document = Apply(x => x.RegisterPatch(MovieName, SetAttributeXPath, new TestSetAttributePatch("Id", "Changed")));

        string[] expected = [.. BaseChildren];
        expected[3] = "Changed";
        CollectionAssert.AreEqual(expected, ListChildren(document));
    }

    [Test]
    public void DisabledPatch_IsNotApplied()
    {
        PrefabComponent prefabComponent = new("TestModule");
        var movieDocument = GetBaseDocument();

        prefabComponent.RegisterPatch(MovieName, ListChildrenXPath, new TestInsertPatch(2));
        prefabComponent.Enable();
        prefabComponent.Disable(typeof(TestInsertPatch));
        prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);
        prefabComponent.Deregister();

        CollectionAssert.AreEqual(BaseChildren, ListChildren(movieDocument));
    }
}
#pragma warning restore CS0618