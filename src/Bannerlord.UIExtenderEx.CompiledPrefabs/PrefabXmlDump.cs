using Bannerlord.BUTR.Shared.Helpers;
using Bannerlord.UIExtenderEx.Runtimes;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.CompiledPrefabs;

/// <summary>
/// Exports fully patched prefab XML documents to disk for corpus regression testing and diagnostic inspection.
/// <para>
/// Activated when the <c>UIEXTENDEREX_DUMP_PREFABS</c> environment variable is set. Emits patched XML structures under
/// <c>GUI/Prefabs/&lt;name&gt;.xml</c> alongside a <c>modules.txt</c> manifest.
/// </para>
/// <para>
/// After the main menu initializes, <see cref="LoadAll"/> can be triggered to parse and export all known prefabs.
/// </para>
/// </summary>
public static class PrefabXmlDump
{
    /// <summary>Environment variable name specifying the destination folder for prefab XML dumps.</summary>
    public const string Variable = "UIEXTENDEREX_DUMP_PREFABS";

    /// <summary>File name for the exported module loading order manifest.</summary>
    public const string ModulesFileName = "modules.txt";

    private static readonly object WriteLock = new();

    private static string? _prefabs;

    /// <summary>Installs the XML export listener when configured via the <see cref="Variable"/> environment variable.</summary>
    public static void Install()
    {
        if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 } directory || _prefabs is not null)
            return;
        try
        {
            // By folder, which is how a module's files are found, and which need not be its id
            Install(directory, ModuleInfoHelper.GetLoadedModules().Select(x => Path.GetFileName(x.Path.TrimEnd('/', '\\'))));
        }
        catch (Exception e)
        {
            Trace.TraceWarning("UIExtenderEx: the prefab dump could not be set up in '{0}': {1}", directory, e.Message);
        }
    }

    /// <summary>Initializes XML exporting into the specified target directory and returns an <see cref="IDisposable"/> token to stop export.</summary>
    public static IDisposable Install(string directory, IEnumerable<string> moduleFolders)
    {
        var prefabs = Path.Combine(directory, "GUI", "Prefabs");
        Directory.CreateDirectory(prefabs);
        File.WriteAllLines(Path.Combine(directory, ModulesFileName), moduleFolders, Encoding.UTF8);
        lock (WriteLock)
        {
            if (_prefabs is not null)
                throw new InvalidOperationException($"Prefabs are dumped into '{_prefabs}' already.");
            _prefabs = prefabs;
        }
        PrefabSource.Parsed += Write;
        Trace.TraceInformation("UIExtenderEx: writing every parsed prefab to '{0}'", prefabs);
        return new Stop();
    }

    /// <summary>
    /// Traverses and parses all prefabs registered with the given <see cref="WidgetFactory"/> to trigger XML export.
    /// </summary>
    /// <returns>A tuple indicating the count of successfully loaded and failed prefabs.</returns>
    public static (int Loaded, int Failed) LoadAll(WidgetFactory widgetFactory)
    {
        if (_prefabs is null)
            return (0, 0);
        var loaded = 0;
        var failed = 0;
        foreach (var name in widgetFactory.GetPrefabNames().ToList())
        {
            try
            {
                widgetFactory.GetCustomType(name);
                loaded++;
            }
            catch (Exception e)
            {
                failed++;
                Trace.TraceWarning("UIExtenderEx: the prefab dump could not load '{0}': {1}", name, e.Message);
            }
        }
        Trace.TraceInformation("UIExtenderEx: the prefab dump loaded {0} prefabs, {1} failed", loaded, failed);
        return (loaded, failed);
    }

    private sealed class Stop : IDisposable
    {
        private bool _stopped;

        public void Dispose()
        {
            if (_stopped)
                return;
            _stopped = true;
            PrefabSource.Parsed -= Write;
            lock (WriteLock)
                _prefabs = null;
        }
    }

    // Serializes file writes to safely handle invocations across worker or UI threads.
    private static void Write(WidgetPrefab prefab, string prefabName, XmlDocument document)
    {
        if (_prefabs is not { } prefabs || string.IsNullOrEmpty(prefabName) || prefabName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return;
        var xml = document.OuterXml;
        lock (WriteLock)
        {
            File.WriteAllText(Path.Combine(prefabs, prefabName + ".xml"), xml, new UTF8Encoding(false));
        }
    }
}