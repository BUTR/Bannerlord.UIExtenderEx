using Bannerlord.UIExtenderEx.Patches;

using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Reflection;
using System.Runtime.CompilerServices;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// The XML loader's own walk over a dotted attribute path, once <see cref="WidgetExtensionsPatch"/> has corrected it.
/// <para>
/// This is the half that makes the matching fix in the generator safe. Fixing only the generator would give an attribute
/// that applies while a prefab is compiled and silently does nothing on the XML it falls back to; fixing both leaves the
/// two agreeing, and agreeing is the point.
/// </para>
/// </summary>
public class PatchedXmlLoaderTests
{
    /// <summary>
    /// <c>WidgetExtensions.GetObjectAndProperty</c> is private, and takes a plain object, so the same chain the generator
    /// is tested against can be walked straight through it.
    /// </summary>
    private static PropertyInfo? WalkPath(object parent, string path)
    {
        var method = AccessTools2.DeclaredMethod(typeof(WidgetExtensions), "GetObjectAndProperty");
        Assert.That(method, Is.Not.Null, "the loader's path walker is gone; the patch needs revisiting");

        var arguments = new[] { parent, path, 0, null, null };
        method!.Invoke(null, arguments);
        return (PropertyInfo?) arguments[4];
    }

    [OneTimeSetUp]
    public void ApplyPatches()
    {
        // UIExtenderEx installs its patches from UIExtender's static constructor; nothing here needs an instance
        RuntimeHelpers.RunClassConstructor(typeof(UIExtender).TypeHandle);
    }

    [Test]
    public void ThePatchApplied()
    {
        Assert.That(WidgetExtensionsPatch.DottedPathsResolveCorrectly, Is.True);
    }

    [Test]
    public void TheLoader_ResolvesASingleSegmentPath()
    {
        var property = WalkPath(new PropertyPathRoot(), "Width");

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.Name, Is.EqualTo("Width"));
    }

    [Test]
    public void TheLoader_ResolvesATwoSegmentPath()
    {
        // The shape shipped prefabs use, and the one the unpatched loader already got right
        var property = WalkPath(new PropertyPathRoot(), "Text.Left");

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.Name, Is.EqualTo("Left"));
        Assert.That(property.DeclaringType, Is.EqualTo(typeof(PropertyPathBranch)));
    }

    [Test]
    public void TheLoader_ResolvesAThreeSegmentPath()
    {
        // Unpatched this asks for Substring(5, 9) => "Left.Marg" and comes back empty
        var property = WalkPath(new PropertyPathRoot(), "Text.Left.Margin");

        Assert.That(property, Is.Not.Null);
        Assert.That(property!.Name, Is.EqualTo("Margin"));
        Assert.That(property.DeclaringType, Is.EqualTo(typeof(PropertyPathLeaf)));
    }

    [Test]
    public void TheLoader_DoesNotRunOffTheEndOfALongFirstSegment()
    {
        // Unpatched this asks for Substring(10, 14) of 21 characters and throws, which on a binding takes the movie down
        Assert.That(() => WalkPath(new PropertyPathRoot(), "Container.Left.Margin"), Throws.Nothing);

        var property = WalkPath(new PropertyPathRoot(), "Container.Left.Margin");
        Assert.That(property, Is.Not.Null);
        Assert.That(property!.Name, Is.EqualTo("Margin"));
    }

    [Test]
    public void TheLoader_StillReportsNothingForAPathThatDoesNotExist()
    {
        Assert.That(WalkPath(new PropertyPathRoot(), "NoSuchProperty"), Is.Null);
        Assert.That(WalkPath(new PropertyPathRoot(), "Text.NoSuchProperty"), Is.Null);
        Assert.That(WalkPath(new PropertyPathRoot(), "Text.Left.NoSuchProperty"), Is.Null);
    }

    [Test]
    public void TheLoaderAndTheGenerator_ResolveTheSamePaths()
    {
        // The property of this whole exercise: whatever a prefab does compiled, it does the same as XML
        foreach (var path in new[]
        {
            "Width", "Text", "Text.Left", "Text.Left.Margin", "Container.Left.Margin",
            "NoSuchProperty", "Text.NoSuchProperty", "Text.Left.NoSuchProperty",
        })
        {
            var loader = WalkPath(new PropertyPathRoot(), path);
            var generator = GauntletUI.CodeGenerator.Widgets.WidgetPropertyPath.Resolve(typeof(PropertyPathRoot), path);

            Assert.That(generator, Is.EqualTo(loader), path);
        }
    }
}
