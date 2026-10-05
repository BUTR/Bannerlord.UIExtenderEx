using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.Settings;

using NUnit.Framework;

using System.Xml;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

public class ViewModelMemberResolutionTests
{
    public static class Outer
    {
        public class Inner { }
    }

    [Test]
    public void CodeTypeName_OfGenericType_IsValidCSharp()
    {
        Assert.That(ViewModelMemberResolution.GetCodeTypeName(typeof(MBBindingList<CodegenItemVM>)),
            Is.EqualTo($"global::TaleWorlds.Library.MBBindingList<global::{typeof(CodegenItemVM).FullName}>"));
    }

    [Test]
    public void CodeTypeName_OfNestedType_UsesDots()
    {
        Assert.That(ViewModelMemberResolution.GetCodeTypeName(typeof(Outer.Inner)),
            Is.EqualTo("global::" + typeof(ViewModelMemberResolutionTests).FullName + ".Outer.Inner"));
    }

    /// <summary>
    /// Verifies that deeply nested generic type arguments format as valid C# syntax across all levels,
    /// avoiding reflection assembly qualifications (e.g. backticks and bracketed assembly metadata).
    /// </summary>
    [Test]
    public void CodeTypeName_OfANestedGeneric_SpellsEveryLevelAsCSharp()
    {
        var name = ViewModelMemberResolution.GetCodeTypeName(typeof(MBBindingList<MBBindingList<CodegenItemVM>>));

        Assert.That(name, Is.EqualTo($"global::TaleWorlds.Library.MBBindingList<global::TaleWorlds.Library.MBBindingList<global::{typeof(CodegenItemVM).FullName}>>"));
        Assert.That(name, Does.Not.Contain("`"));
        Assert.That(name, Does.Not.Contain("["));
    }

    [Test]
    public void CodeTypeName_OfAGenericOverANestedType_UsesDotsInside()
    {
        Assert.That(ViewModelMemberResolution.GetCodeTypeName(typeof(MBBindingList<Outer.Inner>)),
            Is.EqualTo($"global::TaleWorlds.Library.MBBindingList<global::{typeof(ViewModelMemberResolutionTests).FullName}.Outer.Inner>"));
    }

    public class GenericOuter<T>
    {
        public class Inner { }

        public class GenericInner<TInner> { }
    }

    /// <summary>
    /// Verifies that types nested inside generic types distribute generic arguments accurately to each enclosing type level.
    /// </summary>
    [Test]
    public void CodeTypeName_OfATypeNestedInAGeneric_HandsEachLevelItsOwnArguments()
    {
        var outer = "global::" + typeof(ViewModelMemberResolutionTests).FullName + ".GenericOuter";

        Assert.That(ViewModelMemberResolution.GetCodeTypeName(typeof(GenericOuter<int>.Inner)),
            Is.EqualTo($"{outer}<global::System.Int32>.Inner"));
        Assert.That(ViewModelMemberResolution.GetCodeTypeName(typeof(GenericOuter<int>.GenericInner<string>)),
            Is.EqualTo($"{outer}<global::System.Int32>.GenericInner<global::System.String>"));
    }

    [Test]
    public void CodeTypeName_OfArraysAndOpenTypes_IsCSharpOrNothing()
    {
        Assert.That(ViewModelMemberResolution.GetCodeTypeName(typeof(int[,])), Is.EqualTo("global::System.Int32[,]"));
        Assert.That(ViewModelMemberResolution.GetCodeTypeName(typeof(MBBindingList<CodegenItemVM>[])),
            Is.EqualTo($"global::TaleWorlds.Library.MBBindingList<global::{typeof(CodegenItemVM).FullName}>[]"));
        Assert.That(ViewModelMemberResolution.GetCodeTypeName(typeof(MBBindingList<>)), Is.Null, "an open generic names nothing");
    }

    [Test]
    public void MixinAccess_UsesGet()
    {
        Assert.That(ViewModelMemberResolution.GetMixinAccessExpression(typeof(CodegenTestVMMixin), "vm"),
            Is.EqualTo($"global::Bannerlord.UIExtenderEx.ViewModels.ViewModelMixins.Get<global::{typeof(CodegenTestVMMixin).FullName}>(vm)"));
    }

    [Test]
    public void XmlRegistry_HashesTheDocumentContent()
    {
        var a = new XmlDocument();
        a.LoadXml("<Prefab><Window><Widget /></Window></Prefab>");
        var b = new XmlDocument();
        b.LoadXml("<Prefab><Window><Widget Id=\"x\" /></Window></Prefab>");

        PrefabXmlRegistry.Record("RegistryTestA", a);
        PrefabXmlRegistry.Record("RegistryTestB", b);
        PrefabXmlRegistry.Record("RegistryTestA2", a);

        Assert.That(PrefabXmlRegistry.TryGetHash("RegistryTestA", out var hashA), Is.True);
        Assert.That(PrefabXmlRegistry.TryGetHash("RegistryTestB", out var hashB), Is.True);
        Assert.That(PrefabXmlRegistry.TryGetHash("RegistryTestA2", out var hashA2), Is.True);
        Assert.That(hashA, Is.EqualTo(hashA2));
        Assert.That(hashA, Is.Not.EqualTo(hashB));
        Assert.That(PrefabXmlRegistry.TryGetHash("RegistryTestMissing", out _), Is.False);
    }

    [Test]
    public void XmlRegistry_IgnoresEmptyNames()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<Prefab />");

        PrefabXmlRegistry.Record(string.Empty, doc);

        Assert.That(PrefabXmlRegistry.TryGetHash(string.Empty, out _), Is.False);
    }

    [Test]
    public void Settings_DefaultToCompiledPrefabsOn()
    {
        // Asserts standalone defaults when no module configuration overrides them.
        var settings = new SettingsSubModuleXml();

        Assert.That(CompiledPrefabSettings.CompiledPrefabs, Is.True);
        Assert.That(CompiledPrefabSettings.DumpGeneratedCode, Is.False);
        Assert.That(settings.DisableGeneratedPrefabs, Is.False);
        Assert.That(settings.DumpXML, Is.False);
    }
}
