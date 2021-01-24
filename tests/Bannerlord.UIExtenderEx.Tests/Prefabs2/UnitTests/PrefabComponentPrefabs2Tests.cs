using System.Collections.Generic;
using System.Linq;
using System.Xml;

using Bannerlord.UIExtenderEx.Components;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.Tests.Prefabs2.Utilities;

using NUnit.Framework;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs2
{
    public class PrefabComponentPrefabs2Tests
    {
        private static XmlDocument GetBaseDocument()
        {
            XmlDocument document = new();
            document.LoadXml(@"
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
            return document;
        }

        [Test]
        public void RegisterPatch_XmlDocument_InsertAsFirstChild()
        {
            // Arrange
            const string MovieName = "TestMovieName";
            const string XPath = "descendant::OptionsScreenWidget[@Id='Options']/Children";
            XmlDocument patchedDocument = new();
            patchedDocument.LoadXml("<ValidRoot><SomeChild/></ValidRoot>");
            var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, patchedDocument);

            PrefabComponent prefabComponent = new("TestModule");
            var movieDocument = GetBaseDocument();

            // Act
            prefabComponent.RegisterPatch(MovieName, XPath, patch);
            prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);

            // Assert
            var validRootNode = movieDocument.SelectSingleNode("descendant::ValidRoot");
            Assert.IsNotNull(validRootNode);
            Assert.AreEqual("Children", validRootNode!.ParentNode!.Name);
            Assert.AreEqual("SomeChild", validRootNode.FirstChild.Name);
            Assert.AreEqual(validRootNode, validRootNode.ParentNode.FirstChild, $"First child should be ValidRoot. Was {validRootNode.ParentNode.FirstChild.Name}");
        }

        [Test]
        public void RegisterPatch_XmlNode_InsertAsMiddleChild()
        {
            // Arrange
            const string MovieName = "TestMovieName";
            const string XPath = "descendant::OptionsScreenWidget[@Id='Options']/Children";
            XmlDocument patchedDocument = new();
            patchedDocument.LoadXml("<ValidRoot><SomeChild/></ValidRoot>");
            var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, patchedDocument.DocumentElement, 2);

            PrefabComponent prefabComponent = new("TestModule");
            var movieDocument = GetBaseDocument();

            // Act
            prefabComponent.RegisterPatch(MovieName, XPath, patch);
            prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);

            // Assert
            var validRootNode = movieDocument.SelectSingleNode("descendant::ValidRoot");
            Assert.IsNotNull(validRootNode);
            Assert.AreEqual("Children", validRootNode!.ParentNode!.Name);
            Assert.AreEqual("SomeChild", validRootNode.FirstChild.Name);
            Assert.AreEqual(validRootNode, validRootNode.ParentNode.ChildNodes[2], $"Third child should be ValidRoot. Was {validRootNode.ParentNode.FirstChild.Name}");
        }

        [Test]
        public void RegisterPatch_XmlNode_PassXmlDocumentAsXmlNode()
        {
            // Arrange
            const string MovieName = "TestMovieName";
            const string XPath = "descendant::OptionsScreenWidget[@Id='Options']/Children";
            XmlDocument patchedDocument = new();
            patchedDocument.LoadXml("<ValidRoot><SomeChild/></ValidRoot>");
            var patch = PatchCreator.ConstructInsertPatch<XmlNode>(InsertType.Child, patchedDocument, 2);

            PrefabComponent prefabComponent = new("TestModule");
            var movieDocument = GetBaseDocument();

            // Act
            prefabComponent.RegisterPatch(MovieName, XPath, patch);
            prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);

            // Assert
            var validRootNode = movieDocument.SelectSingleNode("descendant::ValidRoot");
            Assert.IsNotNull(validRootNode);
            Assert.AreEqual("Children", validRootNode!.ParentNode!.Name);
            Assert.AreEqual("SomeChild", validRootNode.FirstChild.Name);
            Assert.AreEqual(validRootNode, validRootNode.ParentNode.ChildNodes[2], $"Third child should be ValidRoot. Was {validRootNode.ParentNode.FirstChild.Name}");
        }

        [Test]
        public void RegisterPatch_Text_InsertAsLastChild()
        {
            // Arrange
            const string MovieName = "TestMovieName";
            const string XPath = "descendant::OptionsScreenWidget[@Id='Options']/Children";
            var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, "<ValidRoot><SomeChild/></ValidRoot>", 10);

            PrefabComponent prefabComponent = new("TestModule");
            var movieDocument = GetBaseDocument();

            // Act
            prefabComponent.RegisterPatch(MovieName, XPath, patch);
            prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);

            // Assert
            var validRootNode = movieDocument.SelectSingleNode("descendant::ValidRoot");
            Assert.IsNotNull(validRootNode);
            Assert.AreEqual("Children", validRootNode!.ParentNode!.Name);
            Assert.AreEqual("SomeChild", validRootNode.FirstChild.Name);
            Assert.AreEqual(validRootNode, validRootNode.ParentNode.ChildNodes[validRootNode.ParentNode.ChildNodes.Count - 1], $"Last child should be ValidRoot. Was {validRootNode.ParentNode.FirstChild.Name}");
        }

        [Test]
        public void RegisterPatch_Text_ReplaceRemoveRootNode()
        {
            // Arrange
            const string MovieName = "TestMovieName";
            const string XPath = "descendant::OptionsScreenWidget[@Id='Options']/Children/Standard.TopPanel";
            var patch = PatchCreator.ConstructInsertPatch(InsertType.Replace, "<DiscardedRoot><SomeChild1/><SomeChild2/></DiscardedRoot>", 10, true);

            PrefabComponent prefabComponent = new("TestModule");
            var movieDocument = GetBaseDocument();

            // Act
            prefabComponent.RegisterPatch(MovieName, XPath, patch);
            prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);

            // Assert
            var someChild1Node = movieDocument.SelectSingleNode("descendant::SomeChild1");
            Assert.IsNotNull(someChild1Node);
            Assert.AreEqual("Children", someChild1Node!.ParentNode!.Name);
            Assert.AreEqual(4, someChild1Node!.ParentNode!.ChildNodes.Count);
            Assert.AreEqual("SomeChild1", someChild1Node!.ParentNode!.ChildNodes[0].Name);
            Assert.AreEqual("SomeChild2", someChild1Node!.ParentNode!.ChildNodes[1].Name);
        }

        [Test]
        public void RegisterPatch_XmlNodes_InsertMultipleChildren()
        {
            // Arrange
            const string MovieName = "TestMovieName";
            const string XPath = "descendant::OptionsScreenWidget[@Id='Options']/Children";
            XmlDocument patchedDocument = new();
            patchedDocument.LoadXml("<DiscardedRoot><Child1><InnerChild/></Child1><Child2/><Child3/></DiscardedRoot>");
            var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, patchedDocument!.DocumentElement!.ChildNodes.Cast<XmlNode>());

            PrefabComponent prefabComponent = new("TestModule");
            var movieDocument = GetBaseDocument();

            // Act
            prefabComponent.RegisterPatch(MovieName, XPath, patch);
            prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);

            // Assert
            var child1Node = movieDocument.SelectSingleNode("descendant::Child1");
            Assert.IsNotNull(child1Node);
            Assert.AreEqual("Children", child1Node!.ParentNode!.Name);
            Assert.AreEqual("InnerChild", child1Node.FirstChild.Name);
            Assert.AreEqual("Child1", child1Node.ParentNode.ChildNodes[0].Name, $"First child should be Child1. Was {child1Node.ParentNode.FirstChild.Name}");
            Assert.AreEqual("Child2", child1Node.ParentNode.ChildNodes[1].Name, $"Second child should be Child2. Was {child1Node.ParentNode.FirstChild.Name}");
            Assert.AreEqual("Child3", child1Node.ParentNode.ChildNodes[2].Name, $"Third child should be Child3. Was {child1Node.ParentNode.FirstChild.Name}");
        }

        [Test]
        public void RegisterPatch_XmlNodes_PassXmlDocumentsAsXmlNodes()
        {
            // Arrange
            const string MovieName = "TestMovieName";
            const string XPath = "descendant::OptionsScreenWidget[@Id='Options']/Children";
            XmlDocument patchedDocument1 = new();
            patchedDocument1.LoadXml("<Child1><InnerChild/></Child1> ");
            XmlDocument patchedDocument2 = new();
            patchedDocument2.LoadXml("<Child2/>");
            XmlDocument patchedDocument3 = new();
            patchedDocument3.LoadXml("<Child3/>");
            var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, new List<XmlNode> {patchedDocument1, patchedDocument2, patchedDocument3});

            PrefabComponent prefabComponent = new("TestModule");
            var movieDocument = GetBaseDocument();

            // Act
            prefabComponent.RegisterPatch(MovieName, XPath, patch);
            prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);

            // Assert
            var child1Node = movieDocument.SelectSingleNode("descendant::Child1");
            Assert.IsNotNull(child1Node);
            Assert.AreEqual("Children", child1Node!.ParentNode!.Name);
            Assert.AreEqual("InnerChild", child1Node.FirstChild.Name);
            Assert.AreEqual("Child1", child1Node.ParentNode.ChildNodes[0].Name, $"First child should be Child1. Was {child1Node.ParentNode.FirstChild.Name}");
            Assert.AreEqual("Child2", child1Node.ParentNode.ChildNodes[1].Name, $"Second child should be Child2. Was {child1Node.ParentNode.FirstChild.Name}");
            Assert.AreEqual("Child3", child1Node.ParentNode.ChildNodes[2].Name, $"Third child should be Child3. Was {child1Node.ParentNode.FirstChild.Name}");
        }

        [Test]
        public void RegisterPatch_RemoveComments_ChildComments()
        {
            // Arrange
            const string MovieName = "TestMovieName";
            const string XPath = "descendant::OptionsScreenWidget[@Id='Options']/Children";
            XmlDocument patchedDocument = new();
            patchedDocument.LoadXml("<ValidRoot><!--Child Comment--><SomeChild/></ValidRoot>");
            var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, patchedDocument);

            PrefabComponent prefabComponent = new("TestModule");
            var movieDocument = GetBaseDocument();

            // Act
            prefabComponent.RegisterPatch(MovieName, XPath, patch);
            prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);

            // Assert
            Assert.AreEqual(0, movieDocument.SelectNodes("//comment()")?.Count, $"Remaining comments count: {movieDocument.SelectNodes("//comment()")?.Count}");

            // Validate that every node we did want inserted is actually inserted.
            var validRootNode = movieDocument.SelectSingleNode("descendant::ValidRoot");
            Assert.IsNotNull(validRootNode);
            Assert.AreEqual("Children", validRootNode!.ParentNode!.Name);
            Assert.AreEqual("SomeChild", validRootNode.FirstChild.Name);
            Assert.AreEqual(validRootNode, validRootNode.ParentNode.FirstChild, $"First child should be ValidRoot. Was {validRootNode.ParentNode.FirstChild.Name}");
        }

        [Test]
        public void RegisterPatch_RemoveComments_RootComment()
        {
            // Arrange
            const string MovieName = "TestMovieName";
            const string XPath = "descendant::OptionsScreenWidget[@Id='Options']/Children";
            XmlDocument patchedDocument = new();
            patchedDocument.LoadXml("<DiscardedRoot><ValidRoot><SomeChild/></ValidRoot><!--Root Comment--></DiscardedRoot>");
            var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, patchedDocument!.DocumentElement!.ChildNodes.Cast<XmlNode>());

            PrefabComponent prefabComponent = new("TestModule");
            var movieDocument = GetBaseDocument();

            // Act
            prefabComponent.RegisterPatch(MovieName, XPath, patch);
            prefabComponent.ProcessMovieIfNeeded(MovieName, movieDocument);

            // Assert
            Assert.AreEqual(0, movieDocument.SelectNodes("//comment()")?.Count, $"Remaining comments count: {movieDocument.SelectNodes("//comment()")?.Count}");

            // Validate that every node we did want inserted are actually inserted.
            var validRootNode = movieDocument.SelectSingleNode("descendant::ValidRoot");
            Assert.IsNotNull(validRootNode);
            Assert.AreEqual("Children", validRootNode!.ParentNode!.Name);
            Assert.AreEqual("SomeChild", validRootNode.FirstChild.Name);
            Assert.AreEqual(validRootNode, validRootNode.ParentNode.FirstChild, $"First child should be ValidRoot. Was {validRootNode.ParentNode.FirstChild.Name}");
        }
    }
}