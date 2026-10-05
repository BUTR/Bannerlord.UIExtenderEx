using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.Tests.Utils;

using HarmonyLib.BUTR.Extensions;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Manages a temporary GUI directory containing concrete prefab XML documents loaded through an isolated <see cref="WidgetFactory"/>.
/// </summary>
public sealed class PrefabWorkspace : IDisposable
{
    public const string PlainPrefab = "<Prefab><Window><Widget /></Window></Prefab>";

    public string Root { get; }
    public ResourceDepot ResourceDepot { get; }
    public WidgetFactory WidgetFactory { get; }

    public PrefabWorkspace(params (string Name, string Xml)[] prefabs)
    {
        Root = Path.Combine(Path.GetTempPath(), "UIExtenderEx-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(Root, "GUI", "Prefabs");
        Directory.CreateDirectory(directory);
        foreach (var (name, xml) in prefabs)
            File.WriteAllText(Path.Combine(directory, name + ".xml"), xml);

        RefreshWidgetInfo();

        ResourceDepot = ResourceDepotUtils.Create() ?? throw new InvalidOperationException("ResourceDepot constructor not found");
        ResourceDepot.AddLocation(Root.Replace('\\', '/') + "/", "GUI/");
        ResourceDepot.CollectResources();

        WidgetFactory = new WidgetFactory(ResourceDepot, "Prefabs");
        WidgetFactory.PrefabExtensionContext.AddExtension(new PrefabDatabindingExtension());
        WidgetFactory.Initialize();
    }

    /// <summary>
    /// Generates C# source code for the specified movie and root ViewModel type using the active workspace widget factory.
    /// </summary>
    public List<GeneratedSource> Generate(string movieName, Type viewModelType)
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(WidgetFactory);
        var context = new PrefabCodeGenerator("Bannerlord.UIExtenderEx.Tests.Generated", WidgetFactory, null!, null!);
        context.AddMovie(movieName, viewModelType.FullName, viewModelType);
        return [.. context.GenerateInMemory().Select(x => new GeneratedSource(x.Key, x.Value))];
    }

    /// <summary>
    /// Retrieves the parsed <see cref="WidgetPrefab"/> instance from the underlying widget factory.
    /// </summary>
    public WidgetPrefab Load(string name) => WidgetFactory.GetCustomType(name);

    public static void RefreshWidgetInfo() => WidgetInfo.Refresh();

    /// <summary>
    /// Gets all prefab identifiers currently cached within the factory's live custom types collection.
    /// </summary>
    public IEnumerable<string> LivePrefabNames =>
        AccessTools2.FieldRefAccess<WidgetFactory, IDictionary>("_liveCustomTypes")!(WidgetFactory).Keys.Cast<object>().Select(x => x.ToString()!);

    public void Dispose()
    {
        try { Directory.Delete(Root, true); }
        catch (Exception) { /* ignore */ }
    }
}