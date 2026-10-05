using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Tests.Oracle;
using Bannerlord.UIExtenderEx.Tests.Utils;

using NSubstitute;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.Corpus.Tests;

/// <summary>
/// Loads game UI assets—including prefabs, sprites, fonts, brushes, and widget assemblies—from an installation directory,
/// mirroring the headless resource loading environment used by the game code generator.
/// </summary>
internal sealed class GameInstallation
{
    /// <summary>
    /// Enumerates official TaleWorlds modules and DLCs in standard game load order.
    /// </summary>
    public static readonly string[] OfficialModules = ["Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle", "Multiplayer", "NavalDLC"];

    private static readonly List<string> ProbeDirectories = [];
    private static bool _resolverInstalled;

    public string Directory { get; }
    public IReadOnlyList<string> Modules { get; }
    public ResourceDepot ResourceDepot { get; }
    public SpriteData SpriteData { get; }
    public FontFactory FontFactory { get; }
    public BrushFactory BrushFactory { get; }

    /// <summary>
    /// Gets a <see cref="WidgetFactory"/> instance without extensions, reading attributes as raw string literals.
    /// </summary>
    public WidgetFactory WidgetFactory { get; }

    /// <summary>
    /// Gets a <see cref="WidgetFactory"/> configured with <c>PrefabDatabindingExtension</c> for data-bound movie evaluation.
    /// </summary>
    public WidgetFactory BindingWidgetFactory { get; }

    private GameInstallation(string directory, IReadOnlyList<string> modules)
    {
        Directory = directory;
        Modules = modules;

        LoadWidgetAssemblies(directory, modules);
        WidgetInfo.Refresh();

        var basePath = directory.Replace('\\', '/') + "/";
        ResourceDepot = ResourceDepotUtils.Create() ?? throw new InvalidOperationException("ResourceDepot constructor not found");
        ResourceDepot.AddLocation(basePath, "GUI/GauntletUI/");
        foreach (var module in modules)
            ResourceDepot.AddLocation(basePath, $"Modules/{module}/GUI/");
        // Mounts dumped prefabs after module directories so patched prefabs take precedence by name.
        if (DumpDirectory is { } dump)
            ResourceDepot.AddLocation(dump.Replace('\\', '/').TrimEnd('/') + "/", "GUI/");
        ResourceDepot.CollectResources();

        WidgetFactory = new WidgetFactory(ResourceDepot, "Prefabs");
        WidgetFactory.Initialize();
        BindingWidgetFactory = new WidgetFactory(ResourceDepot, "Prefabs");
        BindingWidgetFactory.PrefabExtensionContext.AddExtension(new TaleWorlds.GauntletUI.Data.PrefabDatabindingExtension());
        BindingWidgetFactory.Initialize();

        SpriteData = new SpriteData("SpriteData");
        SpriteData.Load(ResourceDepot);
        FontFactory = new FontFactory(ResourceDepot);
        FontFactory.LoadAllFonts(SpriteData);
        // Verifies that all required fonts loaded successfully to prevent subsequent text widget instantiation failures.
        var fonts = ResourceDepot.GetFiles("Fonts", ".fnt").Select(Path.GetFileNameWithoutExtension).Distinct(StringComparer.Ordinal).ToList();
        var missing = fonts.Where(x => !FontFactory.GetFonts().Any(font => FontFactory.GetFontName(font) == x)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"The game's fonts did not load: {string.Join(", ", missing)}.");
        BrushFactory = new BrushFactory(ResourceDepot, "Brushes", SpriteData, FontFactory);
        BrushFactory.Initialize();
    }

    /// <summary>
    /// Gets the path specified by the <c>UIEXTENDEREX_ORACLE_PREFAB_DUMP</c> environment variable, containing parsed and patched game prefabs.
    /// </summary>
    public static string? DumpDirectory => Environment.GetEnvironmentVariable("UIEXTENDEREX_ORACLE_PREFAB_DUMP") is { Length: > 0 } dump ? dump : null;

    /// <summary>
    /// Reads the list of dumped prefab names and loaded module directories from the specified dump directory.
    /// </summary>
    public static (List<string> Prefabs, List<string> Modules) ReadDump(string dump) =>
        ([.. System.IO.Directory.EnumerateFiles(Path.Combine(dump, "GUI", "Prefabs"), "*.xml").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, StringComparer.Ordinal)!],
         File.Exists(Path.Combine(dump, "modules.txt")) ? [.. File.ReadAllLines(Path.Combine(dump, "modules.txt")).Where(x => x.Length > 0)] : []);

    /// <summary>
    /// Locates the Mount &amp; Blade II: Bannerlord game installation directory using environment variables or standard Steam paths.
    /// </summary>
    public static string? Find()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"),
            @"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord",
        };
        var directory = candidates.FirstOrDefault(x =>
            !string.IsNullOrEmpty(x) &&
            File.Exists(Path.Combine(x!, "bin", "Win64_Shipping_Client", "TaleWorlds.GauntletUI.dll")) &&
            System.IO.Directory.Exists(Path.Combine(x!, "Modules", "Native", "GUI")));
        // CI sets BANNERLORD_GAME_DIR to '<workspace>/bannerlord', mixing separators; assembly locations are compared
        // against this path as a prefix, and they never do
        return directory is null ? null : Path.GetFullPath(directory);
    }

    /// <summary>
    /// Enumerates third-party installed modules that provide custom prefab definitions, excluding UIExtenderEx itself.
    /// </summary>
    public static List<string> InstalledMods(string directory)
    {
        var modules = Path.Combine(directory, "Modules");
        return [.. System.IO.Directory.GetDirectories(modules)
            .Select(Path.GetFileName)
            .Where(x => x is not null && !OfficialModules.Contains(x) && !x.StartsWith("Bannerlord.UIExtenderEx", StringComparison.OrdinalIgnoreCase))
            .Where(x => System.IO.Directory.Exists(Path.Combine(modules, x!, "GUI", "Prefabs")) &&
                        System.IO.Directory.EnumerateFiles(Path.Combine(modules, x!, "GUI", "Prefabs"), "*.xml", SearchOption.AllDirectories).Any())
            .OrderBy(x => x, StringComparer.Ordinal)!];
    }

    /// <summary>
    /// Collects prefab asset names provided by the specified module under its <c>GUI/Prefabs</c> directory.
    /// </summary>
    public HashSet<string> PrefabsOf(string module) =>
        [.. System.IO.Directory.EnumerateFiles(Path.Combine(Directory, "Modules", module, "GUI", "Prefabs"), "*.xml", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)!];

    /// <summary>
    /// Instantiates and initializes a <see cref="GameInstallation"/> under a system-wide named mutex to prevent font file locking conflicts.
    /// </summary>
    public static GameInstallation Load(string directory, IEnumerable<string> modules)
    {
        using var mutex = new System.Threading.Mutex(false, @"Local\Bannerlord.UIExtenderEx.Tests.GameInstallation");
        try
        {
            mutex.WaitOne();
        }
        catch (System.Threading.AbandonedMutexException)
        {
            // A process that died loading; the mutex is this one's now
        }
        try
        {
            return new(directory, [.. modules.Where(x => System.IO.Directory.Exists(Path.Combine(directory, "Modules", x, "GUI")))]);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    /// <summary>
    /// Creates a headless <see cref="UIContext"/> instance referencing loaded game resources for visual tree comparison.
    /// </summary>
    public UIContext CreateUIContext()
    {
        TestInput.EnsureInitialized();
        var platform = Substitute.For<ITwoDimensionPlatform>();
        platform.ReferenceHeight.Returns(1080f);
        platform.ReferenceWidth.Returns(1920f);
        var twoDimension = new TwoDimensionContext(platform, Substitute.For<ITwoDimensionResourceContext>(), ResourceDepot);
        var context = new UIContext(twoDimension, Substitute.For<TaleWorlds.InputSystem.IInputContext>(), SpriteData, FontFactory, BrushFactory);
        context.Initialize();
        return context;
    }

    /// <summary>
    /// Loads widget-declaring assemblies from game and module binaries into the current application domain.
    /// </summary>
    private static void LoadWidgetAssemblies(string directory, IEnumerable<string> modules)
    {
        var gameBin = Path.Combine(directory, "bin", "Win64_Shipping_Client");
        var binaries = new List<string> { gameBin };
        binaries.AddRange(modules.Select(x => Path.Combine(directory, "Modules", x, "bin", "Win64_Shipping_Client")).Where(System.IO.Directory.Exists));
        // Registers all module binary directories as assembly probe paths to satisfy inter-module dependencies.
        var everyModule = System.IO.Directory.GetDirectories(Path.Combine(directory, "Modules"))
            .Select(x => Path.Combine(x, "bin", "Win64_Shipping_Client")).Where(System.IO.Directory.Exists);
        lock (ProbeDirectories)
        {
            foreach (var binary in binaries.Concat(everyModule).Where(x => !ProbeDirectories.Contains(x)))
                ProbeDirectories.Add(binary);
            if (!_resolverInstalled)
            {
                _resolverInstalled = true;
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            }
        }

        var officialBinaries = new HashSet<string>(OfficialModules.Select(x => Path.Combine(directory, "Modules", x, "bin", "Win64_Shipping_Client")), StringComparer.OrdinalIgnoreCase) { gameBin };
        var gameVersion = ReadGameVersion(directory);
        foreach (var binary in binaries)
        {
            var otherImplementations = OtherImplementations(System.IO.Directory.GetFiles(binary, "*.dll"), gameVersion);
            foreach (var file in System.IO.Directory.GetFiles(binary, "*.dll"))
            {
                if (otherImplementations.Contains(file))
                    continue;
                var name = Path.GetFileNameWithoutExtension(file);
                // Restricts official assembly probing to GUI assemblies while examining all candidate module assemblies.
                if (officialBinaries.Contains(binary) && name.IndexOf("GauntletUI", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Widgets", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (AppDomain.CurrentDomain.GetAssemblies().Any(x => string.Equals(x.GetName().Name, name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                try { LoadOnce(file).GetTypes(); }
                catch (ReflectionTypeLoadException) { /* a partly loadable assembly still contributes what loads */ }
                catch (BadImageFormatException) { /* native */ }
                catch (FileLoadException) { /* a duplicate identity from another folder */ }
            }
        }
    }

    /// <summary>
    /// Parses the game version from <c>Version.xml</c>, or returns <see langword="null"/> if unavailable.
    /// </summary>
    private static Version? ReadGameVersion(string directory)
    {
        try
        {
            var document = System.Xml.Linq.XDocument.Load(Path.Combine(directory, "bin", "Win64_Shipping_Client", "Version.xml"));
            var value = document.Root?.Element("Singleplayer")?.Attribute("Value")?.Value?.TrimStart('v', 'e');
            return Version.TryParse(value, out var version) ? version : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static readonly System.Text.RegularExpressions.Regex VersionedAssembly = new(@"^(?<name>.+?)\.v?(?<version>\d+(?:\.\d+){1,3})$");

    /// <summary>
    /// Identifies superseded multi-version assemblies shipped by modules, retaining only the assembly closest to the current game version.
    /// </summary>
    private static HashSet<string> OtherImplementations(IEnumerable<string> files, Version? gameVersion)
    {
        var others = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var versioned = files
            .Select(file => (File: file, Match: VersionedAssembly.Match(Path.GetFileNameWithoutExtension(file))))
            .Where(x => x.Match.Success && Version.TryParse(x.Match.Groups["version"].Value, out _))
            .Select(x => (x.File, Name: x.Match.Groups["name"].Value, Version: Version.Parse(x.Match.Groups["version"].Value)));
        foreach (var group in versioned.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
        {
            var ordered = group.OrderByDescending(x => x.Version).ToList();
            var kept = ordered.FirstOrDefault(x => gameVersion is null || x.Version <= gameVersion);
            kept = kept.File is null ? ordered[0] : kept;
            others.UnionWith(ordered.Where(x => x.File != kept.File).Select(x => x.File));
        }
        return others;
    }

    /// <summary>
    /// Loads an assembly by identity first before falling back to absolute path loading to avoid duplicate assembly identities.
    /// </summary>
    private static Assembly LoadOnce(string file)
    {
        try
        {
            return Assembly.Load(AssemblyName.GetAssemblyName(file));
        }
        catch (FileNotFoundException)
        {
            return Assembly.LoadFrom(file);
        }
    }

    private static Assembly? Resolve(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        string[] directories;
        lock (ProbeDirectories)
            directories = [.. ProbeDirectories];
        foreach (var directory in directories)
        {
            var path = Path.Combine(directory, name + ".dll");
            if (File.Exists(path))
                return Assembly.LoadFrom(path);
        }
        return null;
    }
}