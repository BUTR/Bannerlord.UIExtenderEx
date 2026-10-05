using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using NUnit.Framework;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// What the generator emits for a binding, per direction.
/// <para>
/// A binding is two-way: the ViewModel property is read into the widget, and a widget property change is written back.
/// The XML loader does both through <c>ViewModel.GetPropertyValue</c> and <c>ViewModel.SetPropertyValue</c>, which reach
/// the property by <c>GetGetMethod()</c> and <c>GetSetMethod()</c>. Both return public accessors or nothing, so what the
/// loader can actually bind is the public surface, whatever the ViewModel's binding table collected. Generated code
/// stands in for that loader and has to draw the line in the same place.
/// </para>
/// </summary>
public class ViewModelBindingTests
{
    private const string MovieName = "BindingMovie";

    private const string Prefab = @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <TextWidget Id=""PublicText"" Text=""@PublicText"" />
        <TextWidget Id=""ReadOnlyText"" Text=""@ReadOnlyText"" />
        <TextWidget Id=""InternalText"" Text=""@InternalText"" />
        <TextWidget Id=""InternalSetterText"" Text=""@InternalSetterText"" />
      </Children>
    </Widget>
  </Window>
</Prefab>";

    private const string ChildMovieName = "BindingChildMovie";

    /// <summary>Navigates into a ViewModel held by a non-public property.</summary>
    private const string ChildPrefab = @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id=""ChildPanel"" DataSource=""{InternalChild}"">
          <Children><TextWidget Id=""ChildText"" Text=""@ChildText"" /></Children>
        </Widget>
      </Children>
    </Widget>
  </Window>
</Prefab>";

    private PrefabWorkspace _workspace = null!;


    [SetUp]
    public void SetUp() => _workspace = new PrefabWorkspace((MovieName, Prefab), (ChildMovieName, ChildPrefab));

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    private GeneratedCode Generate(string movieName) => new(_workspace
        .Generate(movieName, typeof(NonPublicMemberVM))
        .Single(x => x.FileName == movieName + ".gen.cs").Content);

    // --- reading into the widget ----------------------------------------------------------------------------------

    [Test]
    public void EveryBoundProperty_IsReadIntoItsWidget()
    {
        var code = Generate(MovieName);

        Assert.That(code.HasStatement("_widget_0.Text = _datasource_Root.PublicText;"), Is.True);
        Assert.That(code.HasStatement("_widget_1.Text = _datasource_Root.ReadOnlyText;"), Is.True);
        Assert.That(code.HasStatement("_widget_3.Text = _datasource_Root.InternalSetterText;"), Is.True, "a public property reads whatever its setter is");
    }

    [Test]
    public void APropertyTheTypeDoesNotDeclare_IsBoundByName()
    {
        // The type is only what the binding path declares. The instance the binding gets can be a subclass a mod put in
        // a list, or carry properties added at runtime, and the XML loader finds those by name. Generated code asks the
        // same way rather than dropping the binding, which is what leaves a widget at its default value forever.
        using var workspace = new PrefabWorkspace(("UnknownPropertyMovie",
            @"<Prefab><Window><Widget><Children><TextWidget Text=""@NoSuchProperty"" /></Children></Widget></Window></Prefab>"));

        var code = new GeneratedCode(workspace.Generate("UnknownPropertyMovie", typeof(NonPublicMemberVM))
            .Single(x => x.FileName == "UnknownPropertyMovie.gen.cs").Content);

        // Read once, through a binding of its own that holds the member it resolved. The widget half is typed where
        // that provably matches SetWidgetAttribute and keeps the call otherwise, so both spellings are here: the typed
        // branch, and the call in its else-branch reading the same binding the boxed way.
        Assert.That(code.HasStatement("object uiExtenderExValue__widget_0_Text = global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime"
            + ".DynamicMember.Get(_datasource_Root, \"NoSuchProperty\");"), Is.True);
        Assert.That(code.HasStatement("if (uiExtenderExValue__widget_0_Text is global::System.String uiExtenderExAssigned__widget_0_Text)"), Is.True);
        Assert.That(code.HasStatement("_widget_0.Text = uiExtenderExAssigned__widget_0_Text;"), Is.True);
        Assert.That(code.HasStatement("SetWidgetAttribute(this.Context, _widget_0, \"Text\", uiExtenderExValue__widget_0_Text)"), Is.True);
        Assert.That(code.HasStatement("DynamicMember.Set(_datasource_Root, \"NoSuchProperty\", value);"), Is.True);
        // What must never appear is an access to a member that does not exist, which would not compile
        Assert.That(code.HasStatement("_datasource_Root.NoSuchProperty"), Is.False);
        // The test names the value it proved, so nothing casts it again. There is no compiler left that cannot parse
        // that: the bundled Roslyn is asked for LanguageVersion.Latest, and the csc.exe fallback that could not is gone.
        Assert.That(code.HasStatement("= (global::System.String)uiExtenderExValue__widget_0_Text;"), Is.False);
    }

    // --- writing back to the ViewModel ----------------------------------------------------------------------------

    [Test]
    public void APublicSettableProperty_IsWrittenBack()
    {
        var code = Generate(MovieName);

        // The value the widget announced, and one of another type the way the loader writes it, by name
        Assert.That(code.HasStatement("if (value is global::System.String uiExtenderExAnnounced__widget_0_Text)"), Is.True);
        Assert.That(code.HasStatement("_datasource_Root.PublicText = uiExtenderExAnnounced__widget_0_Text;"), Is.True);
        Assert.That(code.HasStatement("DynamicMember.Set(_datasource_Root, \"PublicText\", value);"), Is.True);
    }

    [Test]
    public void AGetOnlyProperty_IsNotWrittenBack()
    {
        var code = Generate(MovieName);

        Assert.That(code.HasStatement("_datasource_Root.ReadOnlyText ="), Is.False);
        Assert.That(code.HasComment("Property in ViewModel does not have a set method"), Is.True);
    }

    [Test]
    public void ANonPublicProperty_IsNotBoundAtAll()
    {
        // ViewModel.GetPropertyValue reads through GetGetMethod(), which is null for a non-public property, so the XML
        // loader throws on the first read. There is no behaviour here to reproduce - the binding does not work in XML -
        // and generating code that made it work would make the same prefab behave differently compiled.
        var code = Generate(MovieName);

        Assert.That(code.HasStatement("_datasource_Root.InternalText"), Is.False);
        Assert.That(code.HasComment("Couldn't find property in ViewModel"), Is.True);
    }

    [Test]
    public void APropertyWithANonPublicSetter_IsReadButNotWrittenBack()
    {
        // The property is public so it reads, but ViewModel.SetPropertyValue writes through GetSetMethod(), which does
        // not see an `internal set`, and skips it. One direction of the binding works in XML; only that one is generated.
        var code = Generate(MovieName);

        Assert.That(code.HasStatement("_widget_3.Text = _datasource_Root.InternalSetterText;"), Is.True);
        Assert.That(code.HasStatement("_datasource_Root.InternalSetterText ="), Is.False);
        Assert.That(code.HasComment("Property in ViewModel does not have a set method"), Is.True);
    }

    // --- navigating into another ViewModel -------------------------------------------------------------------------

    [Test]
    public void AViewModelBehindANonPublicProperty_IsNotNavigated()
    {
        // A DataSource path the loader cannot walk either: it resolves the path with GetPropertyValue, which needs a
        // public getter. The generator declines the movie rather than generating a binding the XML path does not have,
        // and the manager keeps that one movie on XML.
        Assert.That(() => Generate(ChildMovieName), Throws.InstanceOf<Exception>());
    }

    // --- shape of the emitted code ---------------------------------------------------------------------------------

    [Test]
    public void EachBoundWidget_GetsItsOwnChangeHandlerOnce()
    {
        var code = Generate(MovieName);

        foreach (var widget in new[] { "_widget_0", "_widget_1", "_widget_2", "_widget_3" })
        {
            Assert.That(code.CountStatements($"private void HandleWidgetPropertyChangeOf{widget}("), Is.EqualTo(1), widget);
            Assert.That(code.HasStatement($"{widget}.PropertyChanged += PropertyChangedListenerOf{widget};"), Is.True, widget);
            Assert.That(code.HasStatement($"{widget}.PropertyChanged -= PropertyChangedListenerOf{widget};"), Is.True, widget + " unsubscribes");
        }
    }

    [Test]
    public void CodeReachingNonPublicMembers_Compiles()
    {
        // Emitting an access to a non-public member is only right because the generated assembly declares
        // IgnoresAccessChecksTo for every reference it binds against. Without that this would be CS0122 at compile time,
        // which is a worse failure than the missing assignment it replaces.
        var compiler = new RoslynCompiler();

        var references = PrefabReferenceSet.CollectPaths(_workspace.WidgetFactory, typeof(NonPublicMemberVM));
        var sources = _workspace.Generate(MovieName, typeof(NonPublicMemberVM))
            .Concat([IgnoresAccessChecksSource.Create(references)])
            .ToList();

        var result = compiler.Compile(
            CompiledPrefabManager.GetAssemblyName(MovieName, typeof(NonPublicMemberVM), "testbuild0000000"), sources, references);

        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
    }

    [Test]
    public void EverySubscription_HasAMatchingUnsubscription()
    {
        // A handler left attached keeps the movie's widgets alive through the ViewModel after the screen closes
        var code = Generate(MovieName);
        var added = code.Statements.Where(x => x.Contains("+=")).Select(x => x.Replace("+=", "@=")).ToList();
        var removed = code.Statements.Where(x => x.Contains("-=")).Select(x => x.Replace("-=", "@=")).ToList();

        Assert.That(added, Is.Not.Empty);
        Assert.That(added.Distinct(), Is.EquivalentTo(removed.Distinct()));
    }

    // --- the whole output ----------------------------------------------------------------------------------------

    /// <summary>Every file generated for the movie, line for line; see <see cref="GeneratedSnapshot"/>.</summary>
    [Test]
    public Task Snapshot() => GeneratedSnapshot.Verify(_workspace.Generate(MovieName, typeof(NonPublicMemberVM)));
}
