using NUnit.Framework;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests sanitization and resolution of C# identifiers generated from XML attributes and reflection metadata.
/// <para>
/// Verifies compilation and execution for nested ViewModel types (<c>Outer+Inner</c>), non-identifier movie names,
/// and potentially colliding data source paths.
/// </para>
/// </summary>
public class GeneratedIdentifierTests
{
    private PrefabWorkspace? _workspace;
    private TestUIContext? _ui;

    [TearDown]
    public void TearDown()
    {
        _ui?.Dispose();
        _workspace?.Dispose();
    }

    private CompiledMovie Build(string movieName, string xml, System.Type viewModelType)
    {
        _workspace = new PrefabWorkspace((movieName, xml));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(GeneratedIdentifierTests));
        return CompiledMovie.Build(_workspace, movieName, viewModelType, _ui);
    }

    [Test]
    public void ANestedViewModelAndAMovieNameThatIsNoIdentifier_Compile()
    {
        // Configure variant name using ViewModel full name and sanitize movie name into a valid class identifier.
        var movie = Build("Odd-Named Movie", """
<Prefab>
  <Window>
    <LabelWidget Id="Label" Label="@Title" />
  </Window>
</Prefab>
""", typeof(IdentifierHost.NestedVM));

        movie.SetDataSource(new IdentifierHost.NestedVM());

        Assert.That(((LabelWidget) movie.ById("Label")).Label, Is.EqualTo("nested"));
    }

    [Test]
    public void TwoDataSourcePathsThatJoinToOneSpelling_EachGetTheirOwnField()
    {
        // Disambiguate potentially colliding paths such as Root\A_B and Root\A\B into unique field identifiers.
        var movie = Build("ClashMovie", """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget DataSource="{A_B}">
          <Children>
            <LabelWidget Id="Flat" Label="@Title" />
          </Children>
        </Widget>
        <Widget DataSource="{A}">
          <Children>
            <Widget DataSource="{B}">
              <Children>
                <LabelWidget Id="Nested" Label="@Title" />
              </Children>
            </Widget>
          </Children>
        </Widget>
      </Children>
    </Widget>
  </Window>
</Prefab>
""", typeof(ClashVM));

        movie.SetDataSource(new ClashVM());

        Assert.That(((LabelWidget) movie.ById("Flat")).Label, Is.EqualTo("flat"));
        Assert.That(((LabelWidget) movie.ById("Nested")).Label, Is.EqualTo("nested"));
    }
}

public static class IdentifierHost
{
    public class NestedVM : ViewModel
    {
        [DataSourceProperty]
        public string Title => "nested";
    }
}

public class ClashTitleVM : ViewModel
{
    public ClashTitleVM(string title) => Title = title;

    [DataSourceProperty]
    public string Title { get; }
}

public class ClashParentVM : ViewModel
{
    [DataSourceProperty]
    public ClashTitleVM B { get; } = new("nested");
}

public class ClashVM : ViewModel
{
    [DataSourceProperty]
    public ClashTitleVM A_B { get; } = new("flat");

    [DataSourceProperty]
    public ClashParentVM A { get; } = new();
}
