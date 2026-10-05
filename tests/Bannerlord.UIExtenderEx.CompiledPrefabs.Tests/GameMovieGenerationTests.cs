using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GamePrefabs;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.Tests.Utils;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests movie code generation for base game prefabs bound against base game ViewModel types.
/// <para>
/// Verifies generation and Roslyn compilation for complex UI screens including <c>KingdomManagement</c>, which features
/// parent-scope bindings, polymorphic base-typed data sources (<c>CurrentDecision</c>), and untyped nested list bindings.
/// </para>
/// </summary>
public class GameMovieGenerationTests
{
    /// <summary>Specifies campaign module dependencies in engine load order.</summary>
    private static readonly string[] Modules = ["Native", "SandBoxCore", "SandBox", "StoryMode"];

    private ResourceDepot? _resourceDepot;
    private WidgetFactory? _widgetFactory;


    [SetUp]
    public void SetUp()
    {
        if (TestGame.Directory is not { } gameDirectory || TestGame.Bin is not { } bin)
        {
            Assert.Ignore("No game installation; set BANNERLORD_GAME_DIR to run this.");
            return;
        }

        // Load widget assemblies referenced by game prefabs without introducing duplicate type definitions across contexts.
        // Attempt resolution by simple name first to reuse existing assemblies, falling back to disk paths if needed.
        foreach (var path in Directory.GetFiles(bin, "*.GauntletUI*.dll").Concat(Directory.GetFiles(bin, "*.ViewModelCollection.dll")))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            try { Assembly.Load(new AssemblyName(name)); continue; }
            catch (Exception) { /* not one this process already has: load it from the game folder below */ }
            try { Assembly.LoadFrom(path); }
            catch (Exception) { /* a shipped assembly this runtime cannot load is not this test's problem */ }
        }
        WidgetInfo.Refresh();

        _resourceDepot = ResourceDepotUtils.Create() ?? throw new InvalidOperationException("ResourceDepot constructor not found");
        foreach (var module in Modules)
        {
            var location = Path.Combine(gameDirectory, "Modules", module);
            if (Directory.Exists(Path.Combine(location, "GUI")))
                _resourceDepot.AddLocation(location.Replace('\\', '/') + "/", "GUI/");
        }
        _resourceDepot.AddLocation(gameDirectory.Replace('\\', '/') + "/", "GUI/");
        _resourceDepot.CollectResources();

        _widgetFactory = new WidgetFactory(_resourceDepot, "Prefabs");
        _widgetFactory.PrefabExtensionContext.AddExtension(new PrefabDatabindingExtension());
        _widgetFactory.Initialize();
    }

    /// <summary>
    /// Verifies C# source generation for the <c>KingdomManagement</c> movie using dynamic member fallback.
    /// </summary>
    [Test]
    public void KingdomManagement_Generates()
    {
        var sources = Generate("KingdomManagement", KingdomManagementVMType());

        Assert.That(sources, Is.Not.Empty);
        var code = string.Join(Environment.NewLine, sources.Select(x => x.Content));
        Assert.That(code, Does.Contain("DynamicMember."), "the screen only generates because of the by-name fallback");
    }

    /// <summary>
    /// Verifies that code generation correctly emits bindings for the <c>NotableCharacters</c> list in <c>SettlementDecisionPanel.xml</c>,
    /// which resolves members on the derived <c>SettlementDecisionItemVM</c> data source.
    /// </summary>
    [Test]
    public void TheSettlementDecisionPanelsNotablesList_IsGenerated()
    {
        var code = string.Join(Environment.NewLine, Generate("KingdomManagement", KingdomManagementVMType()).Select(x => x.Content));

        Assert.That(code, Does.Contain("\"NotableCharacters\""));
    }

    [Test]
    public void KingdomManagement_Compiles()
    {
        var compiler = new RoslynCompiler();

        var viewModelType = KingdomManagementVMType();
        var references = PrefabReferenceSet.CollectPaths(_widgetFactory!, viewModelType);
        var sources = Generate("KingdomManagement", viewModelType).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();

        var result = compiler.Compile(
            CompiledPrefabManager.GetAssemblyName("KingdomManagement", viewModelType, "acceptance000000"), sources, references);

        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors.Take(40)));
    }

    /// <summary>
    /// Verifies source code generation for additional campaign movies (<c>ClanScreen</c> and <c>Inventory</c>) against base game ViewModels.
    /// </summary>
    [TestCase("ClanScreen", "TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanManagementVM")]
    [TestCase("Inventory", "TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM")]
    public void AGameScreen_Generates(string movieName, string viewModelTypeName)
    {
        var sources = Generate(movieName, GameType(viewModelTypeName));

        Assert.That(sources, Is.Not.Empty);
        Assert.That(sources.Sum(x => x.Content.Length), Is.GreaterThan(0));
    }

    /// <summary>
    /// Verifies that <see cref="GamePrefabRuntime.ConventionsHold"/> validates all pre-compiled movie variants shipped with the game modules.
    /// </summary>
    [Test]
    public void TheGameRuntimesNamingCheck_HoldsForEveryPreCompiledVariantTheGameShips()
    {
        var bins = Modules.Select(x => Path.Combine(TestGame.Directory!, "Modules", x, "bin", "Win64_Shipping_Client")).Where(Directory.Exists).ToList();
        Assembly? Resolve(object? _, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name + ".dll";
            return bins.Append(TestGame.Bin!).Select(x => Path.Combine(x, name)).Where(File.Exists).Select(Assembly.LoadFrom).FirstOrDefault();
        }

        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        try
        {
            // Load module AutoGenerated assemblies containing pre-compiled prefab creators.
            foreach (var path in bins.SelectMany(x => Directory.GetFiles(x, "*.GauntletUI.AutoGenerated*.dll")))
            {
                try { Assembly.LoadFrom(path); }
                catch (Exception e) { NUnit.Framework.TestContext.Out.WriteLine($"not loaded: {Path.GetFileName(path)}: {e.Message}"); }
            }

            var context = _widgetFactory!.GeneratedPrefabContext;
            var stopwatch = Stopwatch.StartNew();
            context.CollectPrefabs();
            var collected = stopwatch.ElapsedMilliseconds;
            var variants = GeneratedPrefabs!(context).Sum(x => x.Value.Count);
            Assume.That(variants, Is.GreaterThan(0), "the game's pre-compiled variants did not load");

            stopwatch.Restart();
            var holds = GamePrefabRuntime.Instance.ConventionsHold(_widgetFactory);
            var firstCheck = stopwatch.ElapsedMilliseconds;

            context.CollectPrefabs();
            stopwatch.Restart();
            var holdsAgain = GamePrefabRuntime.Instance.ConventionsHold(_widgetFactory);
            var secondCheck = stopwatch.ElapsedMilliseconds;

            NUnit.Framework.TestContext.Out.WriteLine($"{variants} variants: collected in {collected} ms, checked in {firstCheck} ms, after a refresh in {secondCheck} ms");
            Assert.That(holds, Is.True, "see the trace for the names that did not resolve");
            Assert.That(holdsAgain, Is.True);
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
            // Clear collected variants to preserve test fixture isolation.
            GeneratedPrefabs!(_widgetFactory!.GeneratedPrefabContext).Clear();
        }
    }

    private static readonly AccessTools.FieldRef<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>? GeneratedPrefabs =
        AccessTools2.FieldRefAccess<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>("_generatedPrefabs");

    private List<GeneratedSource> Generate(string movieName, Type viewModelType)
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(_widgetFactory!);
        var context = new PrefabCodeGenerator("Bannerlord.UIExtenderEx.Tests.Generated", _widgetFactory!, null!, null!);
        context.AddMovie(movieName, viewModelType.FullName, viewModelType);
        return [.. context.GenerateInMemory().Select(x => new GeneratedSource(x.Key, x.Value))];
    }

    private static Type KingdomManagementVMType() =>
        GameType("TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.KingdomManagementVM");

    private static Type GameType(string fullName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(x => x.GetType(fullName, throwOnError: false))
            .OfType<Type>()
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"'{fullName}' was not found in any loaded assembly.");
}
