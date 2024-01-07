﻿using Bannerlord.UIExtenderEx.Tests.Prefabs.IntegrationTests.TestPrefabs;

using NUnit.Framework;

using System.Linq;

using TaleWorlds.Engine.GauntletUI;

namespace Bannerlord.UIExtenderEx.Tests.Prefabs.IntegrationTests;

public class PrefabsTests : BaseTests
{
    private const int Elements = 6;

    [Test]
    public void PrefabsInsertTest()
    {
        var uiExtender = UIExtender.Create("TestModule");
        uiExtender.Register(typeof(PrefabsTests).Assembly);
        uiExtender.Enable();

        var widgetTemplateInsert = UIResourceManager.WidgetFactory.GetCustomType("Insert").RootTemplate;
        var childrenInsert1 = GetChildren(widgetTemplateInsert);
        var childrenInsert2 = GetChildren(childrenInsert1[0]);
        var childrenInsert3 = GetChildren(childrenInsert2[0]);
        Assert.True(childrenInsert3.Count == Elements + 1);
        Assert.True(childrenInsert3[4].Id == "Insert");

        var widgetTemplateReplace = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildren").RootTemplate;
        var childrenReplace1 = GetChildren(widgetTemplateReplace);
        var childrenReplace2 = GetChildren(childrenReplace1[0]);
        var childrenReplace3 = GetChildren(childrenReplace2[0]);
        Assert.True(childrenReplace3.Count == Elements);
        Assert.True(childrenReplace3[2].Id == "Replaced");

        var widgetTemplateInsertAsSiblingAppend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingAppend").RootTemplate;
        var childrenInsertAsSiblingAppend1 = GetChildren(widgetTemplateInsertAsSiblingAppend);
        var childrenInsertAsSiblingAppend2 = GetChildren(childrenInsertAsSiblingAppend1[0]);
        var childrenInsertAsSiblingAppend3 = GetChildren(childrenInsertAsSiblingAppend2[0]);
        Assert.True(childrenInsertAsSiblingAppend3.Count == Elements + 1);
        Assert.True(childrenInsertAsSiblingAppend3[1].Id == "InsertAsSiblingAppend");

        var widgetTemplateInsertAsSiblingPrepend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingPrepend").RootTemplate;
        var childrenInsertAsSiblingPrepend1 = GetChildren(widgetTemplateInsertAsSiblingPrepend);
        var childrenInsertAsSiblingPrepend2 = GetChildren(childrenInsertAsSiblingPrepend1[0]);
        var childrenInsertAsSiblingPrepend3 = GetChildren(childrenInsertAsSiblingPrepend2[0]);
        Assert.True(childrenInsertAsSiblingPrepend3.Count == Elements + 1);
        Assert.True(childrenInsertAsSiblingPrepend3[0].Id == "InsertAsSiblingPrepend");

        var widgetTemplateSetAttribute = UIResourceManager.WidgetFactory.GetCustomType("SetAttribute").RootTemplate;
        var childrenSetAttribute1 = GetChildren(widgetTemplateSetAttribute);
        var childrenSetAttribute2 = GetChildren(childrenSetAttribute1[0]);
        var childrenSetAttribute3 = GetChildren(childrenSetAttribute2[0]);
        Assert.True(childrenSetAttribute3.Count == Elements);
        Assert.True(childrenSetAttribute3[3].AllAttributes.Any(a => a.Key == "CustomAttribute" && a.Value == "Value"));

        uiExtender.Deregister();
    }

    [Test]
    public void PrefabsInsertTestDisabled()
    {
        var uiExtender = UIExtender.Create("TestModule");
        uiExtender.Register(typeof(PrefabsTests).Assembly);
        uiExtender.Enable();
        uiExtender.Disable(typeof(TestPrefabExtensionInsertAsSiblingAppendPatch));

        var widgetTemplateInsert = UIResourceManager.WidgetFactory.GetCustomType("Insert").RootTemplate;
        var childrenInsert1 = GetChildren(widgetTemplateInsert);
        var childrenInsert2 = GetChildren(childrenInsert1[0]);
        var childrenInsert3 = GetChildren(childrenInsert2[0]);
        Assert.True(childrenInsert3.Count == Elements + 1);
        Assert.True(childrenInsert3[4].Id == "Insert");

        var widgetTemplateReplace = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildren").RootTemplate;
        var childrenReplace1 = GetChildren(widgetTemplateReplace);
        var childrenReplace2 = GetChildren(childrenReplace1[0]);
        var childrenReplace3 = GetChildren(childrenReplace2[0]);
        Assert.True(childrenReplace3.Count == Elements);
        Assert.True(childrenReplace3[2].Id == "Replaced");

        var widgetTemplateInsertAsSiblingAppend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingAppend").RootTemplate;
        var childrenInsertAsSiblingAppend1 = GetChildren(widgetTemplateInsertAsSiblingAppend);
        var childrenInsertAsSiblingAppend2 = GetChildren(childrenInsertAsSiblingAppend1[0]);
        var childrenInsertAsSiblingAppend3 = GetChildren(childrenInsertAsSiblingAppend2[0]);
        Assert.False(childrenInsertAsSiblingAppend3.Count == Elements + 1);
        Assert.False(childrenInsertAsSiblingAppend3[1].Id == "InsertAsSiblingAppend");

        var widgetTemplateInsertAsSiblingPrepend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingPrepend").RootTemplate;
        var childrenInsertAsSiblingPrepend1 = GetChildren(widgetTemplateInsertAsSiblingPrepend);
        var childrenInsertAsSiblingPrepend2 = GetChildren(childrenInsertAsSiblingPrepend1[0]);
        var childrenInsertAsSiblingPrepend3 = GetChildren(childrenInsertAsSiblingPrepend2[0]);
        Assert.True(childrenInsertAsSiblingPrepend3.Count == Elements + 1);
        Assert.True(childrenInsertAsSiblingPrepend3[0].Id == "InsertAsSiblingPrepend");

        var widgetTemplateSetAttribute = UIResourceManager.WidgetFactory.GetCustomType("SetAttribute").RootTemplate;
        var childrenSetAttribute1 = GetChildren(widgetTemplateSetAttribute);
        var childrenSetAttribute2 = GetChildren(childrenSetAttribute1[0]);
        var childrenSetAttribute3 = GetChildren(childrenSetAttribute2[0]);
        Assert.True(childrenSetAttribute3.Count == Elements);
        Assert.True(childrenSetAttribute3[3].AllAttributes.Any(a => a.Key == "CustomAttribute" && a.Value == "Value"));

        uiExtender.Deregister();
    }
}