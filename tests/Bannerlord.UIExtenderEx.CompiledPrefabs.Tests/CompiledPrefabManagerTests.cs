using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.Tests.Utils;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Executes synchronous unit tests for the <see cref="CompiledPrefabManager"/> state machine using a mock environment.
/// </summary>
public class CompiledPrefabManagerTests
{
    private const string Movie = "SomeMovie";

    private static readonly AccessTools.FieldRef<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>? GeneratedPrefabs =
        AccessTools2.FieldRefAccess<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>("_generatedPrefabs");

    private sealed class FakeCompiler : ICSharpCompiler
    {
        public string Name { get; set; } = "fake";
        public Queue<Func<CompilationResult>> Results { get; } = new();
        public int Calls { get; private set; }
        public int WarmUps { get; private set; }
        public Exception? WarmUpFailure { get; set; }
        public IReadOnlyList<GeneratedSource> LastSources { get; private set; } = [];

        public CompilationResult Compile(string assemblyName, IReadOnlyList<GeneratedSource> sources, IReadOnlyList<string> referencePaths)
        {
            Calls++;
            LastSources = sources;
            return Results.Count > 0 ? Results.Dequeue()() : CompilationResult.Succeeded([1, 2, 3]);
        }

        public void WarmUp(IReadOnlyList<string> referencePaths)
        {
            WarmUps++;
            if (WarmUpFailure is not null)
                throw WarmUpFailure;
        }
    }

    /// <summary>
    /// Simulates the entry point class of a compiled assembly whose definition collection method is bound as a delegate.
    /// </summary>
    private sealed class FakeCreator
    {
        private readonly string _movie;
        private readonly string _variant;
        public int Collected { get; private set; }

        public FakeCreator(string movie, string variant)
        {
            _movie = movie;
            _variant = variant;
        }

        public void CollectGeneratedPrefabDefinitions(GeneratedPrefabContext generatedPrefabContext)
        {
            Collected++;
            generatedPrefabContext.AddGeneratedPrefab(_movie, _variant, (_, _) => null!);
        }
    }

    private sealed class FakeEnvironment : ICompiledPrefabEnvironment
    {
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Simulates background worker failures when accessing environment properties during compilation.
        /// </summary>
        public bool DumpGeneratedCodeThrows { get; set; }
        private bool _dumpGeneratedCode;
        public bool DumpGeneratedCode
        {
            get => DumpGeneratedCodeThrows ? throw new InvalidOperationException("boom") : _dumpGeneratedCode;
            set => _dumpGeneratedCode = value;
        }
        public bool RecordTimings { get; set; }
        public string? CacheDirectory { get; set; }
        public string? CacheGeneration { get; set; } = "generation-1";
        public IReadOnlyList<string> SeedCaches { get; set; } = [];
        public ICSharpCompiler Compiler { get; set; } = new FakeCompiler();
        public Func<Type, string?> Fingerprint { get; set; } = _ => "fingerprint-a";
        public Exception? GenerationFailure { get; set; }
        public Func<byte[], Action<GeneratedPrefabContext>?> CreatorLoader { get; set; } = _ => null;
        public List<byte[]> LoadedAssemblies { get; } = [];
        public bool RunImmediately { get; set; } = true;
        public Queue<Action> Background { get; } = new();
        public List<string> Warnings { get; } = [];
        public int GenerateCalls { get; private set; }

        public string? FingerprintFailure { get; set; }

        public string? ComputeFingerprint(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string? failure)
        {
            failure = FingerprintFailure;
            return Fingerprint(viewModelType);
        }

        /// <summary>
        /// Gets or sets the post-generation fingerprint computed over the resolved assembly set.
        /// Defaults to the initial lightweight fingerprint unless explicitly overridden.
        /// </summary>
        public Func<Type, string?>? FinalFingerprint { get; set; }

        /// <summary>
        /// Configures <see cref="BeginSnapshot"/> to return <see langword="null"/>, simulating missing XML records for prefab trees.
        /// </summary>
        public bool SnapshotUnavailable { get; set; }

        public List<FakeSnapshot> Snapshots { get; } = [];

        public sealed class FakeSnapshot : IPrefabCompilationSnapshot
        {
            private readonly FakeEnvironment _environment;

            public FakeSnapshot(FakeEnvironment environment, string movieName, Type viewModelType)
            {
                _environment = environment;
                MovieName = movieName;
                ViewModelType = viewModelType;
            }

            public string MovieName { get; }
            public Type ViewModelType { get; }
            public bool IsDisposed { get; private set; }

            public PrefabCompilationInputs CollectInputs()
            {
                var fingerprint = (_environment.FinalFingerprint ?? _environment.Fingerprint)(ViewModelType) ?? string.Empty;
                return new PrefabCompilationInputs([], fingerprint);
            }

            public void Dispose() => IsDisposed = true;
        }

        public IPrefabCompilationSnapshot? BeginSnapshot(WidgetFactory widgetFactory, string movieName, Type viewModelType, out string? failure)
        {
            failure = FingerprintFailure;
            if (SnapshotUnavailable)
                return null;

            var snapshot = new FakeSnapshot(this, movieName, viewModelType);
            Snapshots.Add(snapshot);
            return snapshot;
        }

        public IReadOnlyList<GeneratedSource> GenerateSources(IPrefabCompilationSnapshot snapshot)
        {
            GenerateCalls++;
            if (GenerationFailure is not null)
                throw GenerationFailure;
            return [new GeneratedSource(snapshot.MovieName + ".gen.cs", "// generated"), new GeneratedSource("PrefabCodes.gen.cs", "// creator")];
        }

        public System.Reflection.Assembly LoadAssembly(byte[] assembly)
        {
            LoadedAssemblies.Add(assembly);
            return typeof(FakeEnvironment).Assembly;
        }

        public Action<GeneratedPrefabContext>? CreateCreator(System.Reflection.Assembly assembly) => CreatorLoader(LoadedAssemblies[LoadedAssemblies.Count - 1]);

        public void RunInBackground(Action action)
        {
            if (RunImmediately)
                action();
            else
                Background.Enqueue(action);
        }

        public void Warn(string message) => Warnings.Add(message);
    }

    private string _cacheDirectory = string.Empty;
    private string _seedDirectory = string.Empty;
    private FakeEnvironment _environment = null!;
    private FakeCompiler _compiler = null!;
    private CompiledPrefabManager _manager = null!;
    private WidgetFactory _widgetFactory = null!;
    private CodegenTestVM _dataSource = null!;

    [SetUp]
    public void SetUp()
    {
        _cacheDirectory = Path.Combine(Path.GetTempPath(), "UIExtenderEx-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_cacheDirectory);
        _seedDirectory = _cacheDirectory + "-seeds";

        _compiler = new FakeCompiler();
        _environment = new FakeEnvironment { CacheDirectory = _cacheDirectory, Compiler = _compiler };
        _environment.CreatorLoader = _ => new FakeCreator(Movie, typeof(CodegenTestVM).FullName!).CollectGeneratedPrefabDefinitions;
        _manager = new CompiledPrefabManager(_environment);
        _widgetFactory = new WidgetFactory(ResourceDepotUtils.Create()!, "Prefabs");
        _dataSource = new CodegenTestVM();
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_cacheDirectory, true); } catch (Exception) { /* ignore */ }
        try { Directory.Delete(_seedDirectory, true); } catch (Exception) { /* ignore */ }
        try { Directory.Delete(ProbeDirectory, true); } catch (Exception) { /* ignore */ }
    }

    [Test]
    public void DisabledBySetting_UsesXmlAndDoesNothing()
    {
        _environment.IsEnabled = false;

        Assert.That(TryUse(), Is.False);
        Assert.That(_environment.GenerateCalls, Is.EqualTo(0));
        Assert.That(_compiler.Calls, Is.EqualTo(0));
    }

    [Test]
    public void NullDataSource_UsesXml()
    {
        Assert.That(_manager.TryUseCompiledPrefab(_widgetFactory, Movie, null), Is.False);
        Assert.That(_environment.GenerateCalls, Is.EqualTo(0));
    }

    [Test]
    public void UnknownFingerprint_UsesXmlAndDoesNotGenerate()
    {
        _environment.Fingerprint = _ => null;

        Assert.That(TryUse(), Is.False);
        Assert.That(_environment.GenerateCalls, Is.EqualTo(0));
    }

    [Test]
    public void FirstLoad_CompilesInBackground_NextLoadUsesCompiledPrefab()
    {
        Assert.That(TryUse(), Is.False, "the first load has nothing compiled yet");
        Assert.That(_environment.GenerateCalls, Is.EqualTo(1));
        Assert.That(_compiler.Calls, Is.EqualTo(1));

        Assert.That(TryUse(), Is.True, "the finished compilation is picked up on the next load");
        AssertRegistered(_widgetFactory.GeneratedPrefabContext);
        Assert.That(IsCachedOnDisk("fingerprint-a"), Is.True, "the assembly is cached for the next session");
        Assert.That(_environment.Warnings, Is.Empty);
    }

    [Test]
    public void Compilation_ReceivesTheGeneratedSourcesAndTheAccessChecksAttribute()
    {
        TryUse();

        Assert.That(_compiler.LastSources.Select(x => x.FileName), Is.EquivalentTo(new[] { Movie + ".gen.cs", "PrefabCodes.gen.cs", IgnoresAccessChecksSource.FileName }));
    }

    [Test]
    public void RegisteredPrefab_IsNotRegeneratedOnLaterLoads()
    {
        TryUse();
        TryUse();
        TryUse();

        Assert.That(_environment.GenerateCalls, Is.EqualTo(1));
        Assert.That(_compiler.Calls, Is.EqualTo(1));
    }

    [Test]
    public void CachedAssembly_IsUsedWithoutGeneratingOrCompiling()
    {
        CacheOnDisk("fingerprint-a");

        Assert.That(TryUse(), Is.True);
        Assert.That(_environment.GenerateCalls, Is.EqualTo(0));
        Assert.That(_compiler.Calls, Is.EqualTo(0));
        AssertRegistered(_widgetFactory.GeneratedPrefabContext);
    }

    [Test]
    public void NewBuild_ReplacesTheOlderBuildOfTheSamePair_AndNothingElse()
    {
        CacheOnDisk("fingerprint-old");
        CacheOnDisk("fingerprint-other", typeof(CodegenOtherVM));
        var oldSources = ReportPath(PrefabCache.SourcesDirectoryName);
        Directory.CreateDirectory(oldSources);
        File.WriteAllText(Path.Combine(oldSources, Movie + ".gen.cs"), "// old dump");
        Directory.CreateDirectory(ReportPath(PrefabCache.FailedDirectoryName));

        TryUse();
        Assert.That(TryUse(), Is.True);

        var onDisk = OnDisk();
        Assert.That(onDisk.Entries.Select(x => x.Fingerprint), Is.EquivalentTo(new[] { "fingerprint-a", "fingerprint-other" }),
            "the superseded build is gone, another ViewModel's build is not ours to delete");
        Assert.That(onDisk.Generation, Is.EqualTo("generation-1"));
        Assert.That(Directory.Exists(oldSources), Is.False, "its dumped source");
        Assert.That(Directory.Exists(ReportPath(PrefabCache.FailedDirectoryName)), Is.False, "its failure report");
    }

    [Test]
    public void TheArchive_RecordsWhatEachBuildIsFor()
    {
        TryUse();

        var entry = OnDisk().Entries.Single();
        Assert.That(entry.Movie, Is.EqualTo(Movie));
        Assert.That(entry.Variant, Is.EqualTo(typeof(CodegenTestVM).FullName));
        Assert.That(entry.Fingerprint, Is.EqualTo("fingerprint-a"));
        Assert.That(entry.Sha256, Is.EqualTo(PrefabCache.Sha256([1, 2, 3])));
    }

    /// <summary>
    /// Verifies that the cache archive organizes entries into a directory per movie, an assembly file per ViewModel, and a root manifest.
    /// </summary>
    [Test]
    public void TheArchive_IsLaidOutByMovieAndViewModel()
    {
        TryUse();

        using var archive = new ZipArchive(File.OpenRead(Path.Combine(_cacheDirectory, PrefabCache.FileName)), ZipArchiveMode.Read);
        var pair = Movie + "/" + typeof(CodegenTestVM).FullName;
        Assert.That(archive.Entries.Select(x => x.FullName), Is.EquivalentTo(new[] { "cache.txt", pair + ".dll" }), "a build is its manifest block and its assembly; the fingerprint's inputs are not kept");
    }

    /// <summary>
    /// Verifies that a cached build is recompiled and superseded when any referenced assembly dependency changes,
    /// ensuring updated mod assemblies invalidate older cached prefabs.
    /// </summary>
    [Test]
    public void CachedBuild_WhoseDependencyChanged_IsRebuilt()
    {
        WriteBuild(_cacheDirectory, "fingerprint-a", null, "generation-1", ["Some.Assembly.Nobody.Has:0123456789abcdef"]);

        Assert.That(TryUse(), Is.False);
        Assert.That(_compiler.Calls, Is.EqualTo(1));
        Assert.That(TryUse(), Is.True);
        Assert.That(OnDisk().Entries.Single().Dependencies, Has.None.StartsWith("Some.Assembly.Nobody.Has:"), "the rebuild took its place");
    }

    [Test]
    public void CachedBuild_WhoseDependenciesHold_IsUsed()
    {
        var dependencies = PrefabDependencies.Compose(PrefabDependencies.DescribeInspected([typeof(CodegenTestVM)]), []);
        WriteBuild(_cacheDirectory, "fingerprint-a", null, "generation-1", dependencies);

        Assert.That(TryUse(), Is.True);
        Assert.That(_compiler.Calls, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies that a registered build validates its assembly dependencies on each access and triggers a recompile
    /// when referenced assembly files are missing or modified.
    /// </summary>
    [Test]
    public void RegisteredBuild_WhoseDependencyNoLongerHolds_IsCompiledAgain()
    {
        var gone = CompileStandaloneAssembly("UIExtenderEx.Tests.Gone" + Guid.NewGuid().ToString("N"));
        _compiler.Results.Enqueue(() => CompilationResult.Succeeded([1, 2, 3], [gone]));

        Assert.That(TryUse(), Is.False);
        Assert.That(_compiler.Calls, Is.EqualTo(1));
        Assert.That(TryUse(), Is.False, "the registered build depends on a file no compilation would find");
        Assert.That(_compiler.Calls, Is.EqualTo(2));
        Assert.That(TryUse(), Is.True, "the rebuild depends on nothing that is gone");
        Assert.That(_compiler.Calls, Is.EqualTo(2));
    }

    /// <summary>
    /// Verifies that failed builds retry compilation when <see cref="UIEnvironmentVersion"/> increments without duplicating user-facing warnings.
    /// </summary>
    [Test]
    public void FailedBuild_IsRetriedOnceTheEnvironmentMoves_WithoutWarningAgain()
    {
        _compiler.Results.Enqueue(() => CompilationResult.Failed(["error CS0246: a type of a mod not loaded yet"]));
        _compiler.Results.Enqueue(() => CompilationResult.Failed(["error CS0246: still not loaded"]));

        TryUse();
        TryUse();
        Assert.That(_compiler.Calls, Is.EqualTo(1));

        UIEnvironmentVersion.Touch();
        Assert.That(TryUse(), Is.False);
        Assert.That(_compiler.Calls, Is.EqualTo(2), "retried after the environment moved");
        Assert.That(TryUse(), Is.False);
        Assert.That(_compiler.Calls, Is.EqualTo(2), "and not again until it moves once more");
        Assert.That(_environment.Warnings, Has.Count.EqualTo(1), "the same failure is warned about once");

        UIEnvironmentVersion.Touch();
        TryUse();
        Assert.That(TryUse(), Is.True);
    }

    /// <summary>
    /// Verifies that seeded builds with modified dependencies are discarded in favor of fresh compilation.
    /// </summary>
    [Test]
    public void SeededBuild_WhoseDependencyChanged_IsNotUsed()
    {
        var directory = Path.Combine(_seedDirectory, Guid.NewGuid().ToString("N"));
        WriteBuild(directory, "fingerprint-a", null, "generation-1", ["Some.Assembly.Nobody.Has:0123456789abcdef"]);
        _environment.SeedCaches = [Path.Combine(directory, PrefabCache.FileName)];

        Assert.That(TryUse(), Is.False);
        Assert.That(_compiler.Calls, Is.EqualTo(1));
    }

    [Test]
    public void CacheFromAnotherBuild_IsClearedOnFirstUse()
    {
        CacheOnDisk("fingerprint-a", generation: "generation-0");
        Directory.CreateDirectory(ReportPath(PrefabCache.FailedDirectoryName));

        Assert.That(TryUse(), Is.False, "nothing from another UIExtenderEx or game build is trusted, even with a matching name");
        Assert.That(_compiler.Calls, Is.EqualTo(1));
        Assert.That(Directory.Exists(Path.Combine(_cacheDirectory, PrefabCache.FailedDirectoryName)), Is.False);
        Assert.That(OnDisk().Generation, Is.EqualTo("generation-1"));
        Assert.That(IsCachedOnDisk("fingerprint-a"), Is.True, "the fresh build is cached under the new generation");
    }

    [Test]
    public void FilesOfTheLooseLayout_AreDeletedAndNotUsed()
    {
        File.WriteAllText(Path.Combine(_cacheDirectory, "generation.txt"), "generation-1");
        File.WriteAllBytes(Path.Combine(_cacheDirectory, AssemblyName("fingerprint-a") + ".dll"), [9, 9, 9]);
        File.WriteAllText(Path.Combine(_cacheDirectory, AssemblyName("fingerprint-a") + ".fingerprint.txt"), "input:fingerprint-a\n");

        Assert.That(TryUse(), Is.False);
        Assert.That(_compiler.Calls, Is.EqualTo(1));
        Assert.That(Directory.EnumerateFiles(_cacheDirectory).Select(Path.GetFileName), Is.EquivalentTo(new[] { PrefabCache.FileName }));
    }

    [Test]
    public void UnreadableArchive_IsStartedOver()
    {
        File.WriteAllText(Path.Combine(_cacheDirectory, PrefabCache.FileName), "not a zip");

        Assert.That(TryUse(), Is.False);
        Assert.That(_compiler.Calls, Is.EqualTo(1));
        Assert.That(IsCachedOnDisk("fingerprint-a"), Is.True);
    }

    [Test]
    public void DamagedBuild_IsNotUsed()
    {
        CacheOnDisk("fingerprint-a");
        using (var archive = new ZipArchive(File.Open(Path.Combine(_cacheDirectory, PrefabCache.FileName), FileMode.Open, FileAccess.ReadWrite), ZipArchiveMode.Update))
        {
            var path = Movie + "/" + typeof(CodegenTestVM).FullName + ".dll";
            archive.GetEntry(path)!.Delete();
            using var stream = archive.CreateEntry(path).Open();
            stream.Write([6, 6, 6], 0, 3);
        }

        Assert.That(TryUse(), Is.False, "the bytes do not hash to what the archive recorded for them");
        Assert.That(_environment.LoadedAssemblies.Any(x => x.SequenceEqual(new byte[] { 6, 6, 6 })), Is.False);
        Assert.That(_compiler.Calls, Is.EqualTo(1));
    }

    [Test]
    public void MissingCacheDirectory_IsCreatedWithTheArchive()
    {
        Directory.Delete(_cacheDirectory, true);

        TryUse();

        Assert.That(OnDisk().Generation, Is.EqualTo("generation-1"));
        Assert.That(IsCachedOnDisk("fingerprint-a"), Is.True);
    }

    /// <summary>
    /// Verifies that failure diagnostic reports survive across sessions by preserving generation manifests even when no compilations succeed.
    /// </summary>
    [Test]
    public void FailureReports_SurviveTheNextStart()
    {
        _compiler.Results.Enqueue(() => CompilationResult.Failed(["error CS0000: boom"]));
        TryUse();
        var report = Path.Combine(ReportPath(PrefabCache.FailedDirectoryName), "errors.txt");
        Assert.That(File.Exists(report), Is.True, "test premise");

        var next = new CompiledPrefabManager(new FakeEnvironment { CacheDirectory = _cacheDirectory, Compiler = new FakeCompiler(), RunImmediately = false });
        next.TryUseCompiledPrefab(_widgetFactory, Movie, _dataSource);

        Assert.That(File.Exists(report), Is.True);
    }

    [Test]
    public void SeededBuild_IsUsedWithoutCompiling_AndIsNotCopied()
    {
        _environment.SeedCaches = [Seed("fingerprint-a")];

        Assert.That(TryUse(), Is.True);
        Assert.That(_compiler.Calls, Is.EqualTo(0));
        Assert.That(_environment.GenerateCalls, Is.EqualTo(0));
        Assert.That(OnDisk().Entries, Is.Empty, "a seed is read where it is");
    }

    [Test]
    public void SeededBuild_DropsTheStaleLocalBuildOfThePair()
    {
        CacheOnDisk("fingerprint-old");
        _environment.SeedCaches = [Seed("fingerprint-a")];

        Assert.That(TryUse(), Is.True);
        Assert.That(IsCachedOnDisk("fingerprint-old"), Is.False, "it can never match again and would be preloaded every session");
    }

    [Test]
    public void SeedOfAnotherGeneration_IsIgnored()
    {
        _environment.SeedCaches = [Seed("fingerprint-a", generation: "generation-0")];

        Assert.That(TryUse(), Is.False);
        Assert.That(_compiler.Calls, Is.EqualTo(1));
    }

    [Test]
    public void UnreadableSeed_IsIgnored()
    {
        Directory.CreateDirectory(_seedDirectory);
        var seed = Path.Combine(_seedDirectory, PrefabCache.FileName);
        File.WriteAllText(seed, "not a zip");
        _environment.SeedCaches = [seed, Path.Combine(_seedDirectory, "missing.zip")];

        TryUse();
        Assert.That(TryUse(), Is.True);
        Assert.That(_manager.IsDisabled, Is.False);
    }

    [Test]
    public void WarmUp_PreloadsTheSeededBuildsOfThisGeneration()
    {
        _environment.SeedCaches = [Seed("fingerprint-a"), Seed("fingerprint-other", typeof(CodegenOtherVM), "generation-0")];
        _environment.RunImmediately = false;

        _manager.WarmUpCompilers();
        _environment.Background.Dequeue()();
        Assert.That(_environment.LoadedAssemblies, Has.Count.EqualTo(1), "a build that can never match is not worth a load");

        Assert.That(TryUse(), Is.True);
        Assert.That(_environment.LoadedAssemblies, Has.Count.EqualTo(1), "served from the preload");
    }

    [Test]
    public void FreshCompilation_IsLoadedOnTheWorker_NotWhenTheMovieOpens()
    {
        _environment.RunImmediately = false;

        TryUse();
        Assert.That(_environment.LoadedAssemblies, Is.Empty);

        _environment.Background.Dequeue()();
        Assert.That(_environment.LoadedAssemblies, Has.Count.EqualTo(1), "compiled and loaded on the worker");

        Assert.That(TryUse(), Is.True);
        Assert.That(_environment.LoadedAssemblies, Has.Count.EqualTo(1), "the main thread only registers");
    }

    [Test]
    public void WarmUp_PreloadsCachedAssemblies_SoTheFirstOpenDoesNotLoad()
    {
        CacheOnDisk("fingerprint-a");
        _environment.RunImmediately = false;

        _manager.WarmUpCompilers();
        _environment.Background.Dequeue()();
        Assert.That(_environment.LoadedAssemblies, Has.Count.EqualTo(1));

        Assert.That(TryUse(), Is.True);
        Assert.That(_environment.LoadedAssemblies, Has.Count.EqualTo(1), "served from the preload, not loaded again");
        Assert.That(_compiler.Calls, Is.EqualTo(0));
    }

    /// <summary>
    /// A build of a disabled module references an assembly that never loads. Loading it anyway put it where the game's start-up
    /// type scan found it, which fails on Mono (issue #357).
    /// </summary>
    [Test]
    public void ABuildWhoseReferenceIsGone_IsNeverPreloaded()
    {
        WriteBuild(_cacheDirectory, "fingerprint-a", null, "generation-1", ["Some.Assembly.Nobody.Has:0123456789abcdef"]);
        _environment.RunImmediately = false;

        _manager.WarmUpCompilers();
        _environment.Background.Dequeue()();
        _manager.PreloadDeferredBuilds();
        _environment.Background.Dequeue()();

        Assert.That(_environment.LoadedAssemblies, Is.Empty);
        Assert.That(TryUse(), Is.False, "compiled again instead");
        Assert.That(_environment.LoadedAssemblies, Is.Empty, "not loaded when the movie opens either");
    }

    /// <summary>
    /// A module loader (MCM's) loads its game implementation in its own <c>OnSubModuleLoad</c>, after the start-up preload.
    /// A build referencing it waits for the second stage instead of being loaded before what it references.
    /// </summary>
    [Test]
    public void ABuildWhoseReferenceLoadsLater_IsPreloadedOnceItHas()
    {
        var late = CompileStandaloneAssembly("UIExtenderEx.Tests.Late" + Guid.NewGuid().ToString("N"));
        WriteBuild(_cacheDirectory, "fingerprint-a", null, "generation-1", PrefabDependencies.Compose(new Dictionary<string, string>(), [late]));
        _environment.RunImmediately = false;

        _manager.WarmUpCompilers();
        _environment.Background.Dequeue()();
        Assert.That(_environment.LoadedAssemblies, Is.Empty, "what it references is not loaded yet");

        System.Reflection.Assembly.LoadFrom(late);
        _manager.PreloadDeferredBuilds();
        _environment.Background.Dequeue()();
        Assert.That(_environment.LoadedAssemblies, Has.Count.EqualTo(1));

        Assert.That(TryUse(), Is.True);
        Assert.That(_environment.LoadedAssemblies, Has.Count.EqualTo(1), "served from the preload");
    }

    [Test]
    public void ABuildAMovieLoadedFirst_IsNotLoadedAgainByThePreload()
    {
        CacheOnDisk("fingerprint-a");
        _environment.RunImmediately = false;
        _manager.WarmUpCompilers();
        var preload = _environment.Background.Dequeue();

        Assert.That(TryUse(), Is.True);
        preload();

        Assert.That(_environment.LoadedAssemblies, Has.Count.EqualTo(1), "one build, one load");
    }

    [Test]
    public void DeferredPreload_RunsOnce_AndNotWhenDisabledBySetting()
    {
        _environment.RunImmediately = false;

        _manager.PreloadDeferredBuilds();
        _manager.PreloadDeferredBuilds();
        Assert.That(_environment.Background, Has.Count.EqualTo(1));

        var disabled = new FakeEnvironment { CacheDirectory = _cacheDirectory, IsEnabled = false, RunImmediately = false };
        new CompiledPrefabManager(disabled).PreloadDeferredBuilds();
        Assert.That(disabled.Background, Is.Empty);
    }

    [Test]
    public void CachedAssembly_WithoutPreload_IsLoadedWhenTheMovieOpens()
    {
        CacheOnDisk("fingerprint-a");

        Assert.That(TryUse(), Is.True);
        Assert.That(_environment.LoadedAssemblies, Has.Count.EqualTo(1));
    }

    [Test]
    public void NoCacheDirectory_StillCompilesAndRegisters()
    {
        _environment.CacheDirectory = null;

        TryUse();
        Assert.That(TryUse(), Is.True);
    }

    [Test]
    public void CompileFailure_UsesXml_WarnsOnce_AndIsNotRetried()
    {
        _compiler.Results.Enqueue(() => CompilationResult.Failed(["error CS0000: boom"]));

        Assert.That(TryUse(), Is.False);
        Assert.That(TryUse(), Is.False);
        Assert.That(TryUse(), Is.False);

        Assert.That(_compiler.Calls, Is.EqualTo(1));
        Assert.That(_environment.Warnings, Has.Count.EqualTo(1));
        var failureDirectory = ReportPath(PrefabCache.FailedDirectoryName);
        Assert.That(File.Exists(Path.Combine(failureDirectory, "errors.txt")), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(failureDirectory, "errors.txt")), Does.Contain("boom"));
        Assert.That(File.Exists(Path.Combine(failureDirectory, Movie + ".gen.cs")), Is.True, "the generated source is kept for diagnosis");
    }

    [Test]
    public void CompilerThrowing_IsReportedAsFailure()
    {
        _compiler.Results.Enqueue(() => throw new InvalidOperationException("compiler crashed"));

        TryUse();
        Assert.That(TryUse(), Is.False);

        Assert.That(_compiler.Calls, Is.EqualTo(1));
        Assert.That(_environment.Warnings, Has.Count.EqualTo(1));
        var failureDirectory = ReportPath(PrefabCache.FailedDirectoryName);
        Assert.That(File.ReadAllText(Path.Combine(failureDirectory, "errors.txt")), Does.Contain("compiler crashed"));
    }

    [Test]
    public void UnexpectedFailureInTheCompileJob_IsReportedInsteadOfLeavingThePairPending()
    {
        // Verify that unhandled exceptions inside detached background worker jobs are reported and pending job states are cleared.
        _environment.RunImmediately = false;
        _environment.DumpGeneratedCodeThrows = true;

        Assert.That(TryUse(), Is.False);
        Assert.That(() => _environment.Background.Dequeue()(), Throws.Nothing);

        Assert.That(_manager.IsDisabled, Is.False, "one job going wrong is not the feature going wrong");
        Assert.That(TryUse(), Is.False);
        Assert.That(_environment.Warnings, Has.Count.EqualTo(1), "the job reported a failure the main thread could pick up");
        Assert.That(_environment.Background, Is.Empty, "the pair counts as failed, not as still compiling");
        Assert.That(_compiler.Calls, Is.EqualTo(1), "a pair that failed is not compiled again for the same fingerprint");
    }

    [Test]
    public void FailedFingerprint_IsRetriedWhenTheFingerprintChanges()
    {
        _compiler.Results.Enqueue(() => CompilationResult.Failed(["boom"]));
        TryUse();
        TryUse();
        Assert.That(_compiler.Calls, Is.EqualTo(1));

        _environment.Fingerprint = _ => "fingerprint-b";
        TryUse();
        Assert.That(_compiler.Calls, Is.EqualTo(2));
        Assert.That(TryUse(), Is.True);
    }

    [Test]
    public void UnloadableAssembly_CountsAsFailure()
    {
        _environment.CreatorLoader = _ => null;

        TryUse();
        Assert.That(TryUse(), Is.False);
        Assert.That(_environment.Warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public void PendingCompilation_IsNotScheduledTwice()
    {
        _environment.RunImmediately = false;

        Assert.That(TryUse(), Is.False);
        Assert.That(TryUse(), Is.False);
        Assert.That(_environment.Background, Has.Count.EqualTo(1));
        Assert.That(_environment.GenerateCalls, Is.EqualTo(1));

        _environment.Background.Dequeue()();
        Assert.That(TryUse(), Is.True);
    }

    [Test]
    public void ChangedFingerprint_Recompiles_AndReplacesTheRegistration()
    {
        TryUse();
        Assert.That(TryUse(), Is.True);

        _environment.Fingerprint = _ => "fingerprint-b";
        Assert.That(TryUse(), Is.False, "the registered variant is stale, XML is used until the new one is compiled");
        Assert.That(TryUse(), Is.True);
        Assert.That(_compiler.Calls, Is.EqualTo(2));

        // Verify that the superseded build is deleted from disk.
        var entry = OnDisk().Entries.Single();
        Assert.That(entry.Fingerprint, Is.EqualTo("fingerprint-b"));
    }

    [Test]
    public void OnPrefabsCollected_ReRegistersIntoTheNewContext()
    {
        TryUse();
        TryUse();

        var freshContext = new GeneratedPrefabContext();
        _manager.OnPrefabsCollected(freshContext);

        AssertRegistered(freshContext);
    }

    [Test]
    public void OnPrefabsCollected_PicksUpFinishedCompilations()
    {
        TryUse();

        var freshContext = new GeneratedPrefabContext();
        _manager.OnPrefabsCollected(freshContext);

        AssertRegistered(freshContext);
    }

    [Test]
    public void UnexpectedException_DisablesTheManagerForTheSession()
    {
        _environment.Fingerprint = _ => throw new InvalidOperationException("broken");

        Assert.That(TryUse(), Is.False);
        Assert.That(_manager.IsDisabled, Is.True);
        Assert.That(_environment.Warnings, Has.Count.EqualTo(1));

        _environment.Fingerprint = _ => "fingerprint-a";
        Assert.That(TryUse(), Is.False, "stays disabled even after the cause is gone");
        Assert.That(_environment.GenerateCalls, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies that an unreadable reference falls back to XML for that specific movie without disabling the manager globally.
    /// </summary>
    [Test]
    public void UnreadableReference_UsesXmlForThatMovie_AndKeepsTheManagerAlive()
    {
        var unreadable = new PrefabReferenceException("Some.Mod", @"C:\Modules\Some.Mod\bin\Some.Mod.dll", new FileNotFoundException("gone"));
        _environment.Fingerprint = _ => throw unreadable;

        Assert.That(TryUse(), Is.False);
        Assert.That(TryUse(), Is.False, "tried again, since the file may be back");
        Assert.That(_manager.IsDisabled, Is.False);
        Assert.That(_environment.Warnings, Has.Count.EqualTo(1), "one warning per assembly, not per open");
        Assert.That(_environment.Warnings[0], Does.Contain("Some.Mod"));

        // Compile the movie once the referenced assembly becomes readable.
        _environment.Fingerprint = _ => "fingerprint-a";
        TryUse();
        Assert.That(TryUse(), Is.True);
    }

    [Test]
    public void GenerationFailure_UsesXmlForThatMovieOnly_AndKeepsTheManagerAlive()
    {
        _environment.GenerationFailure = new NullReferenceException("unresolvable binding path");

        Assert.That(TryUse(), Is.False);
        Assert.That(TryUse(), Is.False, "not retried for the same fingerprint");
        Assert.That(_environment.GenerateCalls, Is.EqualTo(1));
        Assert.That(_compiler.Calls, Is.EqualTo(0));
        Assert.That(_environment.Warnings, Has.Count.EqualTo(1));
        Assert.That(_manager.IsDisabled, Is.False);
        var report = Path.Combine(ReportPath(PrefabCache.FailedDirectoryName), "errors.txt");
        Assert.That(File.Exists(report), Is.True, "generation failures are reported on disk like compile failures");
        Assert.That(File.ReadAllText(report), Does.Contain("unresolvable binding path"));

        // Verify that failure on one movie does not block other movies from compiling.
        _environment.GenerationFailure = null;
        _environment.CreatorLoader = _ => new FakeCreator("OtherMovie", typeof(CodegenTestVM).FullName!).CollectGeneratedPrefabDefinitions;
        _manager.TryUseCompiledPrefab(_widgetFactory, "OtherMovie", _dataSource);
        Assert.That(_manager.TryUseCompiledPrefab(_widgetFactory, "OtherMovie", _dataSource), Is.True);
    }

    [Test]
    public void DumpGeneratedCode_WritesSourcesBesideTheArchive()
    {
        _environment.DumpGeneratedCode = true;

        TryUse();

        Assert.That(File.Exists(Path.Combine(ReportPath(PrefabCache.SourcesDirectoryName), Movie + ".gen.cs")), Is.True);
    }

    /// <summary>
    /// Verifies that code generation exceptions are caught internally and fall back to XML without propagating out to the caller.
    /// </summary>
    [Test]
    public void GenerationThrowing_KeepsTheMovieOnXmlWithoutEscaping()
    {
        _environment.GenerationFailure = new NullReferenceException("Object reference not set to an instance of an object.");

        Assert.That(() => TryUse(), Throws.Nothing);
        Assert.That(TryUse(), Is.False, "the movie stays on XML");
        Assert.That(_compiler.Calls, Is.EqualTo(0), "nothing reaches the compiler");
        Assert.That(_environment.Warnings, Is.Not.Empty, "the failure is reported rather than swallowed");
    }

    /// <summary>
    /// Verifies that a generation failure for an individual movie keeps the manager active for other movies.
    /// </summary>
    [Test]
    public void GenerationThrowing_LeavesTheManagerEnabledForOtherMovies()
    {
        _environment.GenerationFailure = new NullReferenceException("boom");
        TryUse();

        Assert.That(_manager.IsDisabled, Is.False);

        _environment.GenerationFailure = null;
        _environment.CreatorLoader = _ => new FakeCreator("OtherMovie", typeof(CodegenTestVM).FullName!).CollectGeneratedPrefabDefinitions;
        _manager.TryUseCompiledPrefab(_widgetFactory, "OtherMovie", _dataSource);

        Assert.That(_manager.TryUseCompiledPrefab(_widgetFactory, "OtherMovie", _dataSource), Is.True, "another movie still compiles");
    }

    [Test]
    public void DifferentViewModels_AreCompiledSeparately()
    {
        var other = new CodegenOtherVM();
        _environment.CreatorLoader = _ => new FakeCreator(Movie, typeof(CodegenOtherVM).FullName!).CollectGeneratedPrefabDefinitions;

        Assert.That(_manager.TryUseCompiledPrefab(_widgetFactory, Movie, other), Is.False);
        Assert.That(_manager.TryUseCompiledPrefab(_widgetFactory, Movie, other), Is.True);
        Assert.That(TryUse(), Is.False, "the CodegenTestVM variant has not been compiled yet");
        Assert.That(_compiler.Calls, Is.EqualTo(2));
    }

    [Test]
    public void WarmUp_RunsOnceInTheBackground()
    {
        _environment.RunImmediately = false;

        _manager.WarmUpCompilers();
        _manager.WarmUpCompilers();

        Assert.That(_environment.Background, Has.Count.EqualTo(1), "scheduled once, on a worker");
        _environment.Background.Dequeue()();
        Assert.That(_compiler.WarmUps, Is.EqualTo(1));
        Assert.That(_compiler.Calls, Is.EqualTo(0), "warm-up is not a real compilation");
    }

    /// <summary>
    /// Verifies that background warm-up JIT-prepares code generator entry points without executing code generation routines.
    /// </summary>
    [Test]
    public void WarmUp_PreparesTheGenerator_WithoutRunningIt()
    {
        _environment.RunImmediately = false;
        var before = GeneratorMethodIsPrepared();

        _manager.WarmUpCompilers();
        _environment.Background.Dequeue()();

        Assert.That(GeneratorMethodIsPrepared(), Is.True, before ? "already prepared before the call, so this proves nothing" : "");
        Assert.That(_compiler.Calls, Is.EqualTo(0), "nothing of the generator is executed, only prepared");
    }

    /// <summary>
    /// Checks whether the target generator method handle has a compiled native entry point rather than a JIT stub.
    /// </summary>
    private static bool GeneratorMethodIsPrepared()
    {
        var method = typeof(PrefabCodeGenerator).GetMethod(nameof(PrefabCodeGenerator.GenerateInMemory));
        Assert.That(method, Is.Not.Null, "the generator entry point this warm-up is meant to cover");
        var before = method!.MethodHandle.GetFunctionPointer();
        RuntimeHelpers.PrepareMethod(method.MethodHandle);
        return method.MethodHandle.GetFunctionPointer() == before;
    }

    [Test]
    public void WarmUp_IsSkippedWhenDisabledBySetting()
    {
        _environment.IsEnabled = false;

        _manager.WarmUpCompilers();

        Assert.That(_compiler.WarmUps, Is.EqualTo(0));
    }

    [Test]
    public void WarmUp_Failure_IsHarmless()
    {
        _compiler.WarmUpFailure = new InvalidOperationException("roslyn missing after all");

        _manager.WarmUpCompilers();

        Assert.That(_manager.IsDisabled, Is.False);
        Assert.That(_environment.Warnings, Is.Empty, "a slower first compile is not worth bothering the user");
        TryUse();
        Assert.That(TryUse(), Is.True, "real compilations still work");
    }

    [Test]
    public void AssemblyName_IsSafeAndUniquePerFingerprint()
    {
        var a = CompiledPrefabManager.GetAssemblyName("Some.Movie-Name", typeof(CodegenTestVM), "0123456789abcdef0123");
        var b = CompiledPrefabManager.GetAssemblyName("Some.Movie-Name", typeof(CodegenTestVM), "fedcba9876543210fedc");

        Assert.That(a, Does.StartWith("Bannerlord.UIExtenderEx.AutoGenerated.Some_Movie_Name.CodegenTestVM."));
        Assert.That(a, Is.Not.EqualTo(b));
        Assert.That(a.Substring(a.LastIndexOf('.') + 1), Has.Length.EqualTo(16));
    }

    [Test]
    public void GeneratedAssemblies_AreRecognisedByName()
    {
        Assert.That(CompiledPrefabManager.IsGeneratedAssemblyName(AssemblyName("fingerprint-a")), Is.True);
        Assert.That(CompiledPrefabManager.IsGeneratedAssemblyName("Bannerlord.UIExtenderEx"), Is.False);
        Assert.That(CompiledPrefabManager.IsGeneratedAssemblyName("Bannerlord.UIExtenderEx.AutoGeneratedButNotOurs"), Is.False);
        Assert.That(CompiledPrefabManager.IsGeneratedAssemblyName(null), Is.False);
    }

    /// <summary>
    /// Verifies that background compilation results are discarded if inputs or fingerprints change before completion.
    /// </summary>
    [Test]
    public void InputsChangingWhileACompilationIsPending_MeansTheResultIsNotUsed()
    {
        _environment.RunImmediately = false;
        Assert.That(TryUse(), Is.False, "the first load has nothing compiled yet");
        var job = _environment.Background.Dequeue();

        // Simulate dependency modification after job preparation.
        _environment.Fingerprint = _ => "fingerprint-b";
        job();

        Assert.That(TryUse(), Is.False, "the finished build was compiled from inputs that are gone");
        Assert.That(_environment.GenerateCalls, Is.EqualTo(2), "and a build for the inputs as they are now is prepared instead");
    }

    [Test]
    public void ASnapshotIsReleased_OnEveryPath()
    {
        // Supply a seed cache since local builds are replaced on each compile pass.
        _environment.SeedCaches = [Seed("fingerprint-cached")];

        // Verify compiled and registered execution path.
        TryUse();
        Assert.That(TryUse(), Is.True);

        // Verify generation failure execution path.
        _environment.Fingerprint = _ => "fingerprint-generation-failure";
        _environment.GenerationFailure = new InvalidOperationException("unresolvable binding path");
        Assert.That(TryUse(), Is.False);
        _environment.GenerationFailure = null;

        // Verify cache hit execution path where prepared inputs exist on disk.
        _environment.Fingerprint = _ => "fingerprint-lightweight";
        _environment.FinalFingerprint = _ => "fingerprint-cached";
        Assert.That(TryUse(), Is.True);

        Assert.That(_environment.Snapshots, Is.Not.Empty);
        Assert.That(_environment.Snapshots.All(x => x.IsDisposed), Is.True, "every prefab a snapshot held has to be released, whatever happened");
    }

    /// <summary>
    /// Verifies that post-generation fingerprint hashes are checked against the cache prior to invoking the compiler.
    /// </summary>
    [Test]
    public void ThePreparedInputsHashingDifferently_AreCheckedAgainstTheCacheBeforeCompiling()
    {
        _environment.Fingerprint = _ => "fingerprint-lightweight";
        _environment.FinalFingerprint = _ => "fingerprint-cached";
        CacheOnDisk("fingerprint-cached");

        Assert.That(TryUse(), Is.True);

        Assert.That(_environment.GenerateCalls, Is.EqualTo(1), "the hash is only known once the sources exist");
        Assert.That(_compiler.Calls, Is.EqualTo(0), "but the build for the prepared inputs was already there");
        AssertRegistered(_widgetFactory.GeneratedPrefabContext);
    }

    /// <summary>
    /// Verifies that cached builds are stored and indexed under the final post-generation fingerprint.
    /// </summary>
    [Test]
    public void TheFinalFingerprint_IsTheOneTheBuildIsCachedUnder()
    {
        _environment.FinalFingerprint = _ => "fingerprint-final";

        Assert.That(TryUse(), Is.False);

        Assert.That(IsCachedOnDisk("fingerprint-final"), Is.True);
        Assert.That(IsCachedOnDisk("fingerprint-a"), Is.False, "not under the cheap check's hash");
    }

    [Test]
    public void ASnapshotThatCannotBeOpened_KeepsTheMovieOnXml()
    {
        _environment.SnapshotUnavailable = true;

        Assert.That(TryUse(), Is.False);
        Assert.That(_environment.GenerateCalls, Is.EqualTo(0));
        Assert.That(_compiler.Calls, Is.EqualTo(0));
    }

    private bool TryUse() => _manager.TryUseCompiledPrefab(_widgetFactory, Movie, _dataSource);

    [Test]
    public void Create_RunsAgainstTheEnvironmentItIsGiven()
    {
        var environment = new FakeEnvironment { IsEnabled = true, RunImmediately = false };

        var manager = CompiledPrefabManager.Create(() => environment);

        Assert.That(manager.IsDisabled, Is.False);
        manager.WarmUpCompilers();
        // Verify that the manager interacts exclusively with the environment supplied by the factory callback.
        Assert.That(environment.Background, Is.Not.Empty, "the given environment was not the one used");
    }

    [Test]
    public void Create_KeepsMoviesOnXml_WhenTheEnvironmentCannotBeBuilt()
    {
        var manager = CompiledPrefabManager.Create(() => throw new InvalidOperationException("no game here"));

        Assert.That(manager.IsDisabled, Is.True);
        Assert.That(manager.TryUseCompiledPrefab(_widgetFactory, Movie, _dataSource), Is.False);
    }

    /// <summary>
    /// Verifies that <see cref="CompiledPrefabRuntime.Manager"/> initializes against the active game environment
    /// without premature singleton construction.
    /// </summary>
    [Test]
    public void HostSingleton_IsBuiltAgainstTheGameEnvironment()
    {
        Assert.That(CompiledPrefabRuntime.Manager.IsDisabled, Is.False);
    }

    // --- timings ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the manager logs each phase of prefab processing (decision, compile, load assembly, register, load movie)
    /// to the session timings file in the cache directory.
    /// </summary>
    [Test]
    public void Timings_RecordEachStep_InTheCacheFolder()
    {
        _environment.RecordTimings = true;

        TryUse();
        TryUse();
        _manager.RecordMovieLoad(Movie, typeof(CodegenTestVM).FullName!, 12.5, "compiled");

        var lines = TimingLines();
        Assert.That(lines.Select(x => x[2]), Is.EqualTo(new[] { "decide", "compile", "load assembly", "register", "decide", "load movie" }));
        Assert.That(lines.Select(x => x[3]), Is.All.EqualTo(Movie));
        Assert.That(lines.Select(x => x[4]), Is.All.EqualTo(typeof(CodegenTestVM).FullName));
        Assert.That(lines[0][6], Does.Contain("compiling in the background"));
        Assert.That(lines[4][6], Does.Contain("compiled variant already registered"));
        Assert.That(lines[5][5], Is.EqualTo("12.5"));
    }

    [Test]
    public void Timings_RecordTheWarmUp()
    {
        _environment.RecordTimings = true;
        CacheOnDisk("fingerprint-a");

        _manager.WarmUpCompilers();

        Assert.That(TimingLines().Select(x => x[2]), Is.EquivalentTo(new[] { "preload", "warm-up generator", "warm-up compiler" }));
    }

    [Test]
    public void Timings_WhenOff_WriteNothing()
    {
        TryUse();
        TryUse();
        _manager.RecordMovieLoad(Movie, typeof(CodegenTestVM).FullName!, 12.5, "compiled");

        Assert.That(Directory.Exists(Path.Combine(_cacheDirectory, PrefabTimings.DirectoryName)), Is.False);
    }

    /// <summary>
    /// Verifies that load timing records XML movie loads when compiled prefabs are disabled, providing baseline comparisons.
    /// </summary>
    [Test]
    public void Timings_RecordLoadsWithTheCompiledRuntimeOff()
    {
        _environment.RecordTimings = true;
        _environment.IsEnabled = false;

        _manager.RecordMovieLoad(Movie, typeof(CodegenTestVM).FullName!, 40, "XML");

        Assert.That(TimingLines().Select(x => x[6]), Is.EqualTo(new[] { "XML" }));
    }

    [Test]
    public void Timings_OfAManagerThatCouldNotBeSetUp_AreNothing()
    {
        var manager = CompiledPrefabManager.Create(() => throw new InvalidOperationException("no game here"));

        Assert.That(() => manager.RecordMovieLoad(Movie, typeof(CodegenTestVM).FullName!, 1, "XML"), Throws.Nothing);
    }

    private List<string[]> TimingLines()
    {
        var file = Directory.GetFiles(Path.Combine(_cacheDirectory, PrefabTimings.DirectoryName)).Single();
        return File.ReadAllLines(file).Skip(1).Select(x => x.Split('\t')).ToList();
    }

    private static string AssemblyName(string fingerprint) => CompiledPrefabManager.GetAssemblyName(Movie, typeof(CodegenTestVM), fingerprint);

    /// <summary>
    /// Resolves the file path for reports matching <c>&lt;kind&gt;/&lt;movie&gt;/&lt;ViewModel full name&gt;</c> in the cache directory.
    /// </summary>
    private string ReportPath(string kind) => Path.Combine(_cacheDirectory, kind, Movie, typeof(CodegenTestVM).FullName!);

    /// <summary>
    /// Writes a mock build to the local archive simulating state left by a prior session.
    /// </summary>
    private void CacheOnDisk(string fingerprint, Type? viewModelType = null, string generation = "generation-1") =>
        WriteBuild(_cacheDirectory, fingerprint, viewModelType, generation);

    /// <summary>
    /// Writes a seeded build archive in an isolated directory simulating a precompiled mod distribution.
    /// </summary>
    private string Seed(string fingerprint, Type? viewModelType = null, string generation = "generation-1")
    {
        var directory = Path.Combine(_seedDirectory, Guid.NewGuid().ToString("N"));
        WriteBuild(directory, fingerprint, viewModelType, generation);
        return Path.Combine(directory, PrefabCache.FileName);
    }

    private string ProbeDirectory => _cacheDirectory + "-probe";

    /// <summary>
    /// Compiles and writes an isolated probe assembly to disk in a separate directory.
    /// </summary>
    private string CompileStandaloneAssembly(string name)
    {
        var result = new RoslynCompiler().Compile(name, [new GeneratedSource("Probe.cs", "public sealed class DependencyProbe { }")],
            PrefabReferenceSet.CollectPaths(null, null));
        Assert.That(result.Success, Is.True, string.Join("\n", result.Errors));
        Directory.CreateDirectory(ProbeDirectory);
        var path = Path.Combine(ProbeDirectory, name + ".dll");
        File.WriteAllBytes(path, result.Assembly!);
        return path;
    }

    private static void WriteBuild(string directory, string fingerprint, Type? viewModelType, string generation, IReadOnlyList<string>? dependencies = null)
    {
        var type = viewModelType ?? typeof(CodegenTestVM);
        var cache = new PrefabCache(directory, generation, []);
        cache.Put(Movie, type.FullName!, fingerprint, [9, 9, 9], dependencies);
        cache.Flush();
    }

    private PrefabCacheContents OnDisk() => PrefabCacheArchive.Read(Path.Combine(_cacheDirectory, PrefabCache.FileName));

    private bool IsCachedOnDisk(string fingerprint) => OnDisk().Entries.Any(x => x.Fingerprint == fingerprint);

    private static void AssertRegistered(GeneratedPrefabContext context)
    {
        Assert.That(GeneratedPrefabs, Is.Not.Null);
        var registered = GeneratedPrefabs!(context);
        Assert.That(registered.ContainsKey(Movie), Is.True, "movie not registered");
        Assert.That(registered[Movie].Keys, Does.Contain(typeof(CodegenTestVM).FullName!));
    }
}
