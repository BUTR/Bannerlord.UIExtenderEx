using Bannerlord.BUTR.Shared.Helpers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Bannerlord.UIExtenderEx.Settings;

/// <summary>
/// Manages configuration settings declared and stored within the module's <c>SubModule.xml</c> manifest.
/// <para>
/// Persists current values via the <c>Value</c> attribute and falls back to <c>Default</c>.
/// Reloads automatically when the manifest file write timestamp changes on disk.
/// </para>
/// </summary>
internal sealed class SettingsSubModuleXml : ISettingsProvider
{
    private const int RefreshIntervalMs = 1000;

    private readonly object _lock = new();
    private readonly Dictionary<string, bool> _values = new();
    private readonly string? _filePath;
    private DateTime _fileWriteTime;
    private int _lastRefreshTick;

    public bool DumpXML { get => Get(nameof(DumpXML), false); set => Set(nameof(DumpXML), value); }
    /// <summary>
    /// Disables native pre-compiled prefab resolution, forcing all prefabs to load directly from XML.
    /// </summary>
    public bool DisableGeneratedPrefabs { get => Get(nameof(DisableGeneratedPrefabs), false); set => Set(nameof(DisableGeneratedPrefabs), value); }

    public SettingsSubModuleXml()
    {
        try
        {
            if (ModuleInfoHelper.GetModuleByType(typeof(SettingsSubModuleXml)) is not { } module) return;

            var filePath = Path.Combine(module.Path, ModuleInfoHelper.SubModuleFile);
            if (!File.Exists(filePath)) return;

            _filePath = filePath;
            Load();
        }
        catch (Exception) { /* the defaults of the properties apply */ }
    }

    /// <summary>Retrieves the current value for <paramref name="property"/>, or returns <paramref name="fallback"/> if not declared.</summary>
    public bool Get(string property, bool fallback)
    {
        RefreshIfChanged();
        lock (_lock)
            return _values.TryGetValue(property, out var value) ? value : fallback;
    }

    /// <summary>Updates the setting value and writes changes to <c>SubModule.xml</c> if declared.</summary>
    public void Set(string property, bool value)
    {
        lock (_lock)
        {
            if (_values.TryGetValue(property, out var current) && current == value) return;
            _values[property] = value;
        }

        Save(property, value);
    }

    /// <summary>
    /// Re-reads settings from the manifest file if its last write timestamp on disk has changed.
    /// Checked at most once per second.
    /// </summary>
    private void RefreshIfChanged()
    {
        if (_filePath is null) return;

        var now = Environment.TickCount;
        if (now - _lastRefreshTick < RefreshIntervalMs) return;
        _lastRefreshTick = now;

        try
        {
            if (File.GetLastWriteTimeUtc(_filePath) == _fileWriteTime) return;
            Load();
        }
        catch (Exception) { /* keep the current values */ }
    }

    private void Load()
    {
        if (_filePath is null) return;

        _fileWriteTime = File.GetLastWriteTimeUtc(_filePath);
        if (SubModuleSettingsXml.Parse(SubModuleSettingsXml.LoadDocument(_filePath)) is not { } declaration) return;

        lock (_lock)
        {
            foreach (var kv in declaration.Values)
            {
                if (bool.TryParse(kv.Value, out var value))
                    _values[kv.Key] = value;
            }
        }
    }

    /// <summary>
    /// Persists a property value as a <c>Value</c> XML attribute in <c>SubModule.xml</c>.
    /// </summary>
    private void Save(string property, bool value)
    {
        if (_filePath is null) return;

        try
        {
            var document = SubModuleSettingsXml.LoadDocument(_filePath);
            if (!SubModuleSettingsXml.SetValues(document, new Dictionary<string, string> { [property] = value ? "true" : "false" }))
                return;

            SubModuleSettingsXml.SaveDocument(document, _filePath);
            _fileWriteTime = File.GetLastWriteTimeUtc(_filePath);
        }
        catch (Exception) { /* read-only module folder, the value applies for this session */ }
    }
}

/// <summary>
/// Represents parsed settings from a <c>SubModule.xml</c> manifest.
/// </summary>
internal sealed record SubModuleSettingsXml(string Id, IReadOnlyDictionary<string, string> Defaults, IReadOnlyDictionary<string, string> Values)
{
    private static readonly HashSet<string> PropertyElements = new(StringComparer.Ordinal) { "Bool", "Integer", "FloatingInteger", "Text", "Dropdown" };

    /// <summary>
    /// Loads the manifest document with whitespace preserved to retain formatting on write.
    /// </summary>
    public static XmlDocument LoadDocument(string filePath)
    {
        var document = new XmlDocument { PreserveWhitespace = true };
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            document.Load(stream);
        return document;
    }

    /// <summary>
    /// Saves the XML document safely using an in-memory buffer to prevent partial file writes.
    /// </summary>
    public static void SaveDocument(XmlDocument document, string filePath)
    {
        using var memory = new MemoryStream();
        document.Save(memory);
        File.WriteAllBytes(filePath, memory.ToArray());
    }

    /// <summary>
    /// Parses settings declarations and active values from the provided manifest document.
    /// </summary>
    public static SubModuleSettingsXml? Parse(XmlDocument document)
    {
        if (FindDeclaration(document) is not var (settings, id)) return null;

        var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in GetPropertyElements(settings))
        {
            var element = kv.Value;
            var @default = element.GetAttribute("Default");
            defaults[kv.Key] = @default;
            values[kv.Key] = element.HasAttribute("Value") ? element.GetAttribute("Value") : @default;
        }
        return new(id, defaults, values);
    }

    /// <summary>
    /// Updates the <c>Value</c> attributes of declared settings in the manifest document.
    /// Returns <see langword="true"/> if any values changed; otherwise, <see langword="false"/>.
    /// </summary>
    public static bool SetValues(XmlDocument document, IReadOnlyDictionary<string, string> values)
    {
        if (FindDeclaration(document) is not var (settings, _)) return false;

        var elements = GetPropertyElements(settings);
        var changed = false;
        foreach (var kv in values)
        {
            if (!elements.TryGetValue(kv.Key, out var element)) continue;
            if (element.HasAttribute("Value") && element.GetAttribute("Value") == kv.Value) continue;

            element.SetAttribute("Value", kv.Value);
            changed = true;
        }
        return changed;
    }

    private static (XmlElement Settings, string Id)? FindDeclaration(XmlDocument document)
    {
        if (document.DocumentElement is not { Name: "Module" } root) return null;

        var moduleId = (root.SelectSingleNode("Id") as XmlElement)?.GetAttribute("value") ?? string.Empty;
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node is not XmlElement { Name: "Settings" } settings) continue;
            var id = settings.HasAttribute("Id") ? settings.GetAttribute("Id") : moduleId;
            if (string.IsNullOrEmpty(id)) continue;
            return (settings, id);
        }
        return null;
    }

    private static Dictionary<string, XmlElement> GetPropertyElements(XmlElement settings)
    {
        var result = new Dictionary<string, XmlElement>(StringComparer.Ordinal);
        Collect(settings, result);
        return result;
    }

    private static void Collect(XmlElement parent, Dictionary<string, XmlElement> result)
    {
        foreach (XmlNode node in parent.ChildNodes)
        {
            if (node is not XmlElement element) continue;

            if (element.Name == "Group")
            {
                Collect(element, result);
                continue;
            }

            if (!PropertyElements.Contains(element.Name)) continue;
            var id = element.GetAttribute("Id");
            if (string.IsNullOrEmpty(id) || result.ContainsKey(id)) continue;
            result[id] = element;
        }
    }
}