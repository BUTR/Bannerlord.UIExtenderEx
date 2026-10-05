using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using NUnit.Framework;

using System.Linq;

namespace Bannerlord.UIExtenderEx.Tests.CodeGenerator;

/// <summary>
/// <see cref="WidgetFactoryLookup.PrefabLease"/> balances the factory's live-use counting for code that only reads prefabs.
/// <para>
/// A prefab the factory holds live is not parsed again, so a prefab left pinned by the generator keeps its pre-patch
/// content for the rest of the session: a mod that enables a patch later would never see it applied.
/// </para>
/// </summary>
public class PrefabLeaseTests
{
    private PrefabWorkspace _workspace = null!;

    [SetUp]
    public void SetUp() => _workspace = new PrefabWorkspace(
        ("LeaseA", PrefabWorkspace.PlainPrefab),
        ("LeaseB", PrefabWorkspace.PlainPrefab));

    [TearDown]
    public void TearDown()
    {
        // The thread-static lease is shared with every other test on this thread; leave none behind
        while (WidgetFactoryLookup.PrefabLease.Current is { } lease)
            lease.Dispose();
        _workspace.Dispose();
    }

    private void Read(string name) => _workspace.WidgetFactory.TryGetCustomTypeIncludingRegistered(name, out _);

    [Test]
    public void Read_OutsideALease_StaysPinned()
    {
        // The behaviour the lease exists to contain, and the reason reading through the factory is not free
        Read("LeaseA");

        Assert.That(_workspace.LivePrefabNames, Is.EquivalentTo(new[] { "LeaseA" }));
    }

    [Test]
    public void Lease_ReleasesEveryPrefabItRead()
    {
        using (WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory))
        {
            Read("LeaseA");
            Read("LeaseB");
            Assert.That(_workspace.LivePrefabNames, Is.EquivalentTo(new[] { "LeaseA", "LeaseB" }), "held while the lease is open");
        }

        Assert.That(_workspace.LivePrefabNames, Is.Empty);
    }

    [Test]
    public void Lease_ReleasesOncePerRead()
    {
        // The factory counts uses, so two reads owe two releases; releasing once would leave the prefab pinned
        using (var lease = WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory))
        {
            Read("LeaseA");
            Read("LeaseA");
            Assert.That(lease.Acquired, Is.EqualTo(new[] { "LeaseA", "LeaseA" }));
        }

        Assert.That(_workspace.LivePrefabNames, Is.Empty);
    }

    [Test]
    public void APinningLease_AnswersALaterReadWithTheObjectItRead()
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory, pin: true);
        _workspace.WidgetFactory.TryGetCustomTypeIncludingRegistered("LeaseA", out var first);
        _workspace.WidgetFactory.TryGetCustomTypeIncludingRegistered("LeaseA", out var second);

        Assert.That(second, Is.SameAs(first));
        Assert.That(lease.Acquired, Is.EqualTo(new[] { "LeaseA" }), "the second read was answered from the lease, not acquired again");
        Assert.That(lease.Pinned!.Keys, Is.EquivalentTo(new[] { "LeaseA" }));
    }

    [Test]
    public void ALeaseThatDoesNotPin_HoldsNothing()
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory);
        Read("LeaseA");

        Assert.That(lease.Pinned, Is.Null);
    }

    [Test]
    public void Lease_OverAnUnknownName_AcquiresNothing()
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory);

        Assert.That(_workspace.WidgetFactory.TryGetCustomTypeIncludingRegistered("NoSuchPrefab", out _), Is.False);
        Assert.That(lease.Acquired, Is.Empty);
    }

    [Test]
    public void NestedLeases_EachReleaseTheirOwnReads()
    {
        using (WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory))
        {
            Read("LeaseA");
            using (WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory))
            {
                Read("LeaseB");
            }

            Assert.That(_workspace.LivePrefabNames, Is.EquivalentTo(new[] { "LeaseA" }), "the inner lease released only its own read");
        }

        Assert.That(_workspace.LivePrefabNames, Is.Empty);
    }

    [Test]
    public void ALeaseReleasesWhatItAcquired_EvenWhenDisposedOutOfOrder()
    {
        // Review finding 2: Dispose returns without releasing anything unless the lease is the innermost one, so an outer
        // lease disposed first drops its reads on the floor and pins them for the session. Both call sites are non-nested
        // `using var` today, which is why nothing has noticed; the invariant a disposable owes is unconditional.
        var outer = WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory);
        Read("LeaseA");
        var inner = WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory);
        Read("LeaseB");

        outer.Dispose();
        inner.Dispose();

        Assert.That(_workspace.LivePrefabNames, Is.Empty, "disposing a lease has to release its reads whatever order the leases end in");
    }

    [Test]
    public void Lease_DisposedTwice_ReleasesOnlyOnce()
    {
        Read("LeaseA");
        var lease = WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory);
        Read("LeaseA");

        lease.Dispose();
        lease.Dispose();

        // The read from before the lease is still owed by whoever made it
        Assert.That(_workspace.LivePrefabNames, Is.EquivalentTo(new[] { "LeaseA" }));
    }

    [Test]
    public void Lease_LeavesNoThreadStateBehind()
    {
        using (WidgetFactoryLookup.PrefabLease.Begin(_workspace.WidgetFactory))
        {
            Assert.That(WidgetFactoryLookup.PrefabLease.Current, Is.Not.Null);
        }

        Assert.That(WidgetFactoryLookup.PrefabLease.Current, Is.Null);
    }

    [Test]
    public void Generation_ReadsThroughTheLeaseAndReleasesEverything()
    {
        using var workspace = new PrefabWorkspace(
            ("LeaseMovie", "<Prefab><Window><Widget><Children><LeasedPart /></Children></Widget></Window></Prefab>"),
            ("LeasedPart", PrefabWorkspace.PlainPrefab));

        var code = workspace.Generate("LeaseMovie", typeof(StructureVM));

        Assert.That(code.Any(x => x.FileName == "LeaseMovie.gen.cs"), Is.True, "test premise: the movie generated");
        Assert.That(workspace.LivePrefabNames, Is.Empty, "the movie and the prefab it embeds are both released");
    }
}
