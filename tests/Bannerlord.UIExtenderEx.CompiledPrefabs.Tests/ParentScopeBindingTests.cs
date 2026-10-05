using NUnit.Framework;

using System.Linq;
using System.Text.RegularExpressions;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies code generation and runtime binding resolution for parent-scope bindings (<c>{..}</c>) declared at the movie root.
/// <para>
/// Because GauntletUI path resolution treats parent escapes at the root level as referring to the root data source itself,
/// the code generator collapses these references into the root data source scope rather than emitting invalid parent lookup variables.
/// </para>
/// </summary>
public class ParentScopeBindingTests
{
    /// <summary>
    /// Verifies that <see cref="ViewModel.GetViewModelAtPath(BindingPath)"/> resolves simplified parent escape paths back to the root ViewModel instance.
    /// </summary>
    [Test]
    public void TheLoader_ResolvesAnEscapedPathToTheRootViewModel()
    {
        var viewModel = new CodegenTestVM();
        var escapedOnce = new BindingPath("Root").Append(new BindingPath("..")).Simplify();
        var escapedTwice = escapedOnce.Append(new BindingPath("..")).Simplify();

        Assert.That(escapedOnce.Path, Is.Empty);
        Assert.That(escapedTwice.Path, Is.EqualTo(".."));

        Assert.That(viewModel.GetViewModelAtPath(new BindingPath("Root")), Is.SameAs(viewModel));
        Assert.That(viewModel.GetViewModelAtPath(escapedOnce), Is.SameAs(viewModel), "one hop above the root is the root");
        Assert.That(viewModel.GetViewModelAtPath(escapedTwice), Is.SameAs(viewModel), "two hops above the root is still the root");
    }

    /// <summary>
    /// Verifies that a parent-scope binding placed at root alongside a sibling widget correctly resolves to the root data source.
    /// </summary>
    [Test]
    public void AParentScopeBindingAtRootWithASibling_BindsToTheRoot()
    {
        using var workspace = new PrefabWorkspace(("SiblingMovie", """
<Prefab><Window><Widget><Children>
  <Widget DataSource="{..}"><Children><TextWidget Text="@Title" /></Children></Widget>
  <TextWidget Text="@Title" />
</Children></Widget></Window></Prefab>
"""));

        var code = string.Join("\n", workspace.Generate("SiblingMovie", typeof(CodegenTestVM)).Select(x => x.Content));

        AssertOnlyTheRootDataSource(code);
    }

    /// <summary>
    /// Verifies that nested parent-scope bindings at the root level collapse into the root data source.
    /// </summary>
    [Test]
    public void NestedParentScopeBindings_BindToTheRoot()
    {
        using var workspace = new PrefabWorkspace(("NestedEscapeMovie", """
<Prefab><Window><Widget DataSource="{..}"><Children>
  <Widget DataSource="{..}"><Children><TextWidget Text="@Title" /></Children></Widget>
</Children></Widget></Window></Prefab>
"""));

        var code = string.Join("\n", workspace.Generate("NestedEscapeMovie", typeof(CodegenTestVM)).Select(x => x.Content));

        AssertOnlyTheRootDataSource(code);
    }

    /// <summary>
    /// Verifies that a parent-scope binding on the root widget itself resolves to the root data source.
    /// </summary>
    [Test]
    public void TheRootWidgetBindingAtAParentScope_BindsToTheRoot()
    {
        using var workspace = new PrefabWorkspace(("RootEscapeMovie", """
<Prefab><Window><Widget DataSource="{..}"><Children><TextWidget Text="@Title" /></Children></Widget></Window></Prefab>
"""));

        var code = string.Join("\n", workspace.Generate("RootEscapeMovie", typeof(CodegenTestVM)).Select(x => x.Content));

        AssertOnlyTheRootDataSource(code);
    }

    /// <summary>
    /// Asserts that the generated source code contains only the root data source variable, confirming that escaped paths collapsed into the root.
    /// </summary>
    private static void AssertOnlyTheRootDataSource(string code)
    {
        Assert.That(code, Does.Contain("_datasource_Root"));

        var dataSourceVariables = Regex.Matches(code, @"_datasource_\w*").Cast<Match>().Select(x => x.Value).Distinct().ToList();
        Assert.That(dataSourceVariables, Is.EquivalentTo(new[] { "_datasource_Root" }),
            "an escaped path is the root data source, not a second one");
    }
}
