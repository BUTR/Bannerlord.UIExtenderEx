using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.ResourceManager;

using NUnit.Framework;

using System;
using System.Linq;
using System.Xml;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs
{
    /// <summary>
    /// Tests fingerprint cache invalidation mechanics. Verifies that changes to resolved widget types, mixin
    /// registrations, and prefab dependencies invalidate fingerprints and Roslyn reference sets while preserving
    /// caches across unrelated prefab modifications.
    /// </summary>
    public class FingerprintInvalidationTests
    {
        private const string Movie = "InvalidationMovie";
        private const string OtherMovie = "InvalidationOtherMovie";
        private const string WidgetName = nameof(Swapped.InvalidationSwappableWidget);

        private static readonly string MoviePrefab =
            $@"<Prefab><Window><Widget><Children><{WidgetName} Id=""Swappable"" /></Children></Widget></Window></Prefab>";

        private PrefabWorkspace? _workspace;
        private UIExtender? _extender;

        [SetUp]
        public void SetUp()
        {
            // Apply game engine patches, including prefab XML hash recording.
            _extender = UIExtender.Create("TestModule.CompiledPrefabs.Invalidation");
            _workspace = new PrefabWorkspace(
                (Movie, MoviePrefab),
                (OtherMovie, PrefabWorkspace.PlainPrefab),
                ("InvalidationUnrelated", PrefabWorkspace.PlainPrefab));
            PrefabFingerprint.ClearCache();
            PrefabReferenceSet.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            _extender?.Deregister();
            _workspace?.Dispose();
            PrefabFingerprint.ClearCache();
            PrefabReferenceSet.ClearCache();
        }

        private string? Compute(string movieName = Movie) =>
            TestFingerprint.Compute(_workspace!.WidgetFactory, movieName, typeof(CodegenTestVM));

        /// <summary>
        /// Verifies that re-registering a widget name with a different widget type from the same assembly updates
        /// the fingerprint and regenerates code using the new type.
        /// </summary>
        [Test]
        public void ReplacingAWidgetWithAnotherTypeOfTheSameAssembly_ChangesTheFingerprintAndTheGeneratedType()
        {
            Assert.That(typeof(Swapped.InvalidationSwappableWidget).Assembly,
                Is.EqualTo(typeof(SwappedAgain.InvalidationSwappableWidget).Assembly), "test premise: one assembly, two classes, one name");

            WidgetFactoryManager.Register(typeof(Swapped.InvalidationSwappableWidget));
            var before = Compute();
            var codeBefore = Generate();

            WidgetFactoryManager.Register(typeof(SwappedAgain.InvalidationSwappableWidget));
            var after = Compute();
            var codeAfter = Generate();

            Assert.That(codeBefore, Does.Contain("new global::" + typeof(Swapped.InvalidationSwappableWidget).FullName + "(this.Context)"));
            Assert.That(codeAfter, Does.Contain("new global::" + typeof(SwappedAgain.InvalidationSwappableWidget).FullName + "(this.Context)"));
            Assert.That(after, Is.Not.EqualTo(before), "the generated code builds a different widget class, so the cached build cannot stand");
        }

        [Test]
        public void ResolvedWidgetTypes_AreNamedInTheInputs()
        {
            WidgetFactoryManager.Register(typeof(Swapped.InvalidationSwappableWidget));

            PrefabFingerprint.Compute(_workspace!.WidgetFactory, Movie, typeof(CodegenTestVM), out var inputs);

            var swapped = typeof(Swapped.InvalidationSwappableWidget);
            Assert.That(inputs, Does.Contain($"widget:{WidgetName}={swapped.FullName}, {swapped.Assembly.GetName().Name}\n"));
            // Track assembly build versions via MVID in dependency records rather than fingerprint keys.
            Assert.That(inputs, Does.Not.Contain("Version="));
        }

        /// <summary>
        /// Verifies that reversing registration order for conflicting mixins updates the fingerprint and selects
        /// the corresponding mixin implementation.
        /// </summary>
        [Test]
        public void ReversingTwoConflictingMixinRegistrations_ChangesTheFingerprintAndTheSelectedMixin()
        {
            var resolver = new MixinMemberResolver();

            var (firstOrder, firstWinner) = WithMixins([typeof(CodegenTestVMMixin), typeof(CodegenTestVMSecondMixin)], resolver);
            var (reversed, reversedWinner) = WithMixins([typeof(CodegenTestVMSecondMixin), typeof(CodegenTestVMMixin)], resolver);
            var (sameAgain, _) = WithMixins([typeof(CodegenTestVMMixin), typeof(CodegenTestVMSecondMixin)], resolver);

            Assert.That(firstWinner, Is.EqualTo(typeof(CodegenTestVMSecondMixin)));
            Assert.That(reversedWinner, Is.EqualTo(typeof(CodegenTestVMMixin)), "test premise: the order decides which mixin a contested property comes from");
            Assert.That(reversed, Is.Not.EqualTo(firstOrder));
            Assert.That(sameAgain, Is.EqualTo(firstOrder), "an unchanged order must not rebuild anything");
        }

        private (string? Fingerprint, Type? Winner) WithMixins(Type[] mixins, MixinMemberResolver resolver)
        {
            var extender = UIExtender.Create("TestModule.CompiledPrefabs.Invalidation.Order");
            try
            {
                extender.Register(mixins);
                extender.Enable();
                resolver.ResolveProperty(typeof(CodegenTestVM), nameof(CodegenTestVMMixin.MixinText), out var winner);
                return (Compute(), winner);
            }
            finally
            {
                extender.Deregister();
            }
        }

        [Test]
        public void MixinRegistrations_AreGroupedByTargetInTheInputs()
        {
            var extender = UIExtender.Create("TestModule.CompiledPrefabs.Invalidation.Grouping");
            try
            {
                extender.Register([typeof(CodegenTestVMMixin), typeof(CodegenTestVMSecondMixin)]);
                extender.Enable();

                PrefabFingerprint.Compute(_workspace!.WidgetFactory, Movie, typeof(CodegenTestVM), out var inputs);
                var lines = inputs.Split(['\n'], StringSplitOptions.RemoveEmptyEntries).ToList();
                var target = lines.FindIndex(x => x.StartsWith("mixintarget:" + PrefabFingerprint.DescribeType(typeof(CodegenTestVM)), StringComparison.Ordinal));

                Assert.That(target, Is.GreaterThanOrEqualTo(0), "the target ViewModel heads its own group");
                Assert.That(lines[target + 1], Does.Contain(PrefabFingerprint.DescribeType(typeof(CodegenTestVMMixin))));
                Assert.That(lines[target + 2], Does.Contain(PrefabFingerprint.DescribeType(typeof(CodegenTestVMSecondMixin))), "in resolver order, not sorted");
            }
            finally
            {
                extender.Deregister();
            }
        }

        /// <summary>
        /// Verifies that deregistering mixins increments <see cref="UIEnvironmentVersion.Version"/> to trigger recompilation.
        /// </summary>
        [Test]
        public void DeregisteringAMixin_MovesTheEnvironmentVersion()
        {
            var extender = UIExtender.Create("TestModule.CompiledPrefabs.Invalidation.Lifecycle");
            extender.Register([typeof(CodegenTestVMMixin)]);
            extender.Enable();
            var version = UIEnvironmentVersion.Version;

            extender.Deregister();

            Assert.That(UIEnvironmentVersion.Version, Is.GreaterThan(version));
        }

        /// <summary>
        /// Verifies that distinct movies referencing the same ViewModel type maintain separate, independent reference cache entries.
        /// </summary>
        [Test]
        public void TwoMoviesSharingAViewModel_KeepSeparateReferenceCacheEntries()
        {
            var factory = _workspace!.WidgetFactory;
            var first = PrefabFingerprint.GetPrefabSection(factory, Movie)!;
            var second = PrefabFingerprint.GetPrefabSection(factory, OtherMovie)!;

            var firstSet = PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), first);
            var secondSet = PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), second);

            Assert.That(PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), first), Is.SameAs(firstSet), "the other movie must not have evicted this one");
            Assert.That(PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), second), Is.SameAs(secondSet));
        }

        /// <summary>
        /// Verifies that modifying a prefab closure invalidates and replaces the cached reference set.
        /// </summary>
        [Test]
        public void AClosureSayingSomethingElse_ReplacesTheOldEntry()
        {
            var factory = _workspace!.WidgetFactory;
            var section = PrefabFingerprint.GetPrefabSection(factory, Movie)!;
            var set = PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), section);

            var changed = section with { Text = section.Text + "prefab:InvalidationExtra:0000" };
            Assert.That(changed.Key, Is.Not.EqualTo(section.Key), "test premise: a different closure");

            PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), changed);

            Assert.That(PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), section), Is.Not.SameAs(set), "the older closure of this pair is gone");
        }

        /// <summary>
        /// Verifies that rebuilding a prefab section that produces an identical closure retains the cached assembly reference set.
        /// </summary>
        [Test]
        public void ARebuiltClosureSayingTheSameThing_KeepsTheCollectedAssemblySet()
        {
            var factory = _workspace!.WidgetFactory;
            var section = PrefabFingerprint.GetPrefabSection(factory, Movie)!;
            var set = PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), section);

            var changed = new XmlDocument();
            changed.LoadXml(MoviePrefab.Replace("Swappable", "SwappableChanged"));
            PrefabXmlRegistry.Record(Movie, changed);
            var reparsed = PrefabFingerprint.GetPrefabSection(factory, Movie)!;
            Assert.That(reparsed, Is.Not.SameAs(section), "test premise: it could not be confirmed and was rebuilt");
            Assert.That(reparsed.Key, Is.EqualTo(section.Key), "and the tree it describes is unchanged");

            Assert.That(PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), reparsed), Is.SameAs(set));
        }

        [Test]
        public void UnchangedInputsAndUnrelatedPrefabChanges_KeepTheCachedAnswer()
        {
            var factory = _workspace!.WidgetFactory;
            var before = Compute();
            var section = PrefabFingerprint.GetPrefabSection(factory, Movie)!;
            var set = PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), section);

            var unrelated = new XmlDocument();
            unrelated.LoadXml("<Prefab><Window><Widget Id=\"changed\" /></Window></Prefab>");
            PrefabXmlRegistry.Record("InvalidationUnrelated", unrelated);

            Assert.That(Compute(), Is.EqualTo(before));
            Assert.That(PrefabFingerprint.GetPrefabSection(factory, Movie), Is.SameAs(section));
            Assert.That(PrefabReferenceSet.Collect(factory, typeof(CodegenTestVM), section), Is.SameAs(set));
        }

        private string Generate() =>
            string.Join("\n", _workspace!.Generate(Movie, typeof(CodegenTestVM)).Select(x => x.Content));
    }
}

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.Swapped
{
    /// <summary>Represents a test widget used to verify type replacement within the same assembly.</summary>
    public class InvalidationSwappableWidget : Widget
    {
        public InvalidationSwappableWidget(UIContext context) : base(context) { }
    }
}

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.SwappedAgain
{
    /// <summary>Represents an alternative test widget registered under the same name to verify type replacement invalidation.</summary>
    public class InvalidationSwappableWidget : Widget
    {
        public InvalidationSwappableWidget(UIContext context) : base(context) { }
    }
}
