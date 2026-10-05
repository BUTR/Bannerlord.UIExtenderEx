using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;

using NUnit.Framework;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies dynamic member resolution for scalar property bindings when a base-typed data source holds a derived ViewModel instance,
/// matching polymorphic UI patterns such as <c>SettlementDecisionPanel</c>'s <c>CurrentDecision</c>.
/// <para>
/// Ensures compiled prefabs resolve derived properties dynamically on assignment, property change notifications,
/// and two-way write-back bindings without requiring static declared presence on the base class.
/// </para>
/// </summary>
public class UnresolvedScalarBindingTests
{
    private const string Movie = "UnresolvedScalarMovie";

    private const string Prefab = """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <Widget Id="Declared" HoveredCursorState="@BaseText" />
        <Widget Id="Subclass" HoveredCursorState="@DerivedText" />
        <Widget Id="SubclassFlag" IsVisible="@DerivedFlag" />
        <Widget Id="Writeback" IsVisible="@WritableFlag" />
        <Widget Id="Absent" HoveredCursorState="@NoSuchPropertyAnywhere" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""";

    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private IDynamicMemberHost? _previousHost;


    [SetUp]
    public void SetUp()
    {
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        _workspace = new PrefabWorkspace((Movie, Prefab));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(UnresolvedScalarBindingTests));
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui.Dispose();
        _workspace.Dispose();
    }

    private CompiledMovie Build() => CompiledMovie.Build(_workspace, Movie, typeof(LoaderBaseVM), _ui);

    [Test]
    public void ASubclassMemberIsBound_WhenTheDataSourceIsAssigned()
    {
        var movie = Build();

        movie.SetDataSource(new LoaderDerivedVM());

        Assert.That(movie.ById("Declared").HoveredCursorState, Is.EqualTo("base"), "the resolved binding still works");
        Assert.That(movie.ById("Subclass").HoveredCursorState, Is.EqualTo("derived"));
        Assert.That(movie.ById("SubclassFlag").IsVisible, Is.True);
    }

    /// <summary>
    /// Verifies that assigning a base instance lacking derived members evaluates missing bindings to null without throwing.
    /// </summary>
    [Test]
    public void AnAbsentMemberLeavesTheWidgetWithNull()
    {
        var movie = Build();

        movie.SetDataSource(new LoaderBaseVM());

        Assert.That(movie.ById("Subclass").HoveredCursorState, Is.Null);
        Assert.That(movie.ById("Absent").HoveredCursorState, Is.Null);
    }

    /// <summary>
    /// Verifies that dynamic derived properties update active widget state upon receiving property change notifications.
    /// </summary>
    [Test]
    public void ASubclassMemberFollowsAnOrdinaryNotification()
    {
        var movie = Build();
        var viewModel = new NotifyingDerivedVM();
        movie.SetDataSource(viewModel);

        Assert.That(movie.ById("Subclass").HoveredCursorState, Is.EqualTo("first"));

        viewModel.SetDerivedText("second");

        Assert.That(movie.ById("Subclass").HoveredCursorState, Is.EqualTo("second"));
    }

    /// <summary>
    /// Verifies that strongly typed property change notifications deliver payload values directly to widget properties
    /// without re-querying the underlying property getter.
    /// </summary>
    [Test]
    public void ATypedNotificationPayloadReachesTheWidget_WithoutRereadingTheProperty()
    {
        var movie = Build();
        var viewModel = new NotifyingDerivedVM();
        movie.SetDataSource(viewModel);

        viewModel.AnnounceDerivedTextAs("a value the getter does not return");

        Assert.That(movie.ById("Subclass").HoveredCursorState, Is.EqualTo("a value the getter does not return"));
        Assert.That(viewModel.DerivedText, Is.EqualTo("first"), "the property itself never moved");
    }

    [Test]
    public void ATypedBoolNotificationPayloadReachesTheWidget()
    {
        var movie = Build();
        var viewModel = new NotifyingDerivedVM();
        movie.SetDataSource(viewModel);

        Assert.That(movie.ById("SubclassFlag").IsVisible, Is.True);

        viewModel.AnnounceDerivedFlagAs(false);

        Assert.That(movie.ById("SubclassFlag").IsVisible, Is.False);
    }

    /// <summary>
    /// Verifies that statically resolved bindings also accept payload values directly, preserving consistency when
    /// write-backs or property calculations mutate state during notification handling.
    /// </summary>
    [Test]
    public void AResolvedBindingTakesThePayloadToo()
    {
        var movie = Build();
        var viewModel = new NotifyingDerivedVM();
        movie.SetDataSource(viewModel);

        viewModel.AnnounceBaseTextAs("a payload the getter does not return");

        Assert.That(movie.ById("Declared").HoveredCursorState, Is.EqualTo("a payload the getter does not return"));
    }

    [Test]
    public void TheWidgetWritesBackByName()
    {
        var movie = Build();
        var viewModel = new NotifyingDerivedVM();
        movie.SetDataSource(viewModel);
        Assert.That(viewModel.WritableFlag, Is.False);

        movie.ById("Writeback").IsVisible = true;

        Assert.That(viewModel.WritableFlag, Is.True);
    }

    /// <summary>
    /// Verifies that two-way write-back attempts to missing members on the runtime instance fail silently without throwing.
    /// </summary>
    [Test]
    public void AWritebackToAnAbsentMemberDoesNothing()
    {
        var movie = Build();
        movie.SetDataSource(new LoaderBaseVM());

        Assert.That(() => movie.ById("Writeback").IsVisible = true, Throws.Nothing);
    }

    /// <summary>
    /// Verifies that alternating between different derived data source instances rebinds dynamic properties correctly
    /// and detaches listeners from previous instances.
    /// </summary>
    [Test]
    public void SwappingBetweenSubclassesRebinds_AndOldSourcesStopReaching()
    {
        var movie = Build();
        var first = new NotifyingDerivedVM();
        movie.SetDataSource(first);
        Assert.That(movie.ById("Subclass").HoveredCursorState, Is.EqualTo("first"));

        var second = new LoaderOtherDerivedVM();
        movie.SetDataSource(second);
        Assert.That(movie.ById("Subclass").HoveredCursorState, Is.EqualTo("other derived"));

        movie.SetDataSource(null);
        first.SetDerivedText("this must not be seen");
        Assert.That(movie.ById("Subclass").HoveredCursorState, Is.EqualTo("other derived"),
            "a detached source no longer drives the widget");

        movie.SetDataSource(first);
        Assert.That(movie.ById("Subclass").HoveredCursorState, Is.EqualTo("this must not be seen"),
            "and reattaching reads it again");
    }
}
