using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.Oracle.Fuzzing;

/// <summary>
/// Represents an individual widget node in a fuzzed prefab tree, including type name, ordered attributes, and children.
/// </summary>
internal sealed class FuzzWidget
{
    public FuzzWidget(string type) => Type = type;

    public string Type { get; set; }
    public List<KeyValuePair<string, string>> Attributes { get; } = [];
    public List<FuzzWidget> Children { get; } = [];

    /// <summary>
    /// Gets or sets the item template widget for bound list controls, or <see langword="null"/> when not applicable.
    /// </summary>
    public FuzzWidget? ItemTemplate { get; set; }

    public bool LogicalChildrenLocation { get; set; }

    public FuzzWidget Clone()
    {
        var clone = new FuzzWidget(Type) { ItemTemplate = ItemTemplate?.Clone(), LogicalChildrenLocation = LogicalChildrenLocation };
        clone.Attributes.AddRange(Attributes);
        clone.Children.AddRange(Children.Select(x => x.Clone()));
        return clone;
    }

    /// <summary>
    /// Traverses the widget hierarchy in parent-first preorder, including all children and item templates.
    /// </summary>
    public IEnumerable<FuzzWidget> Descendants()
    {
        yield return this;
        foreach (var widget in Children.SelectMany(x => x.Descendants()))
            yield return widget;
        if (ItemTemplate is not null)
        {
            foreach (var widget in ItemTemplate.Descendants())
                yield return widget;
        }
    }
}

/// <summary>
/// Represents a generated prefab document model, including header declarations and the root widget tree.
/// </summary>
internal sealed class FuzzPrefab
{
    public FuzzPrefab(string name, FuzzWidget root)
    {
        Name = name;
        Root = root;
    }

    public string Name { get; }
    public FuzzWidget Root { get; set; }
    public List<KeyValuePair<string, string>> Parameters { get; } = [];

    /// <summary>
    /// Gets constant definitions as attribute lists, ordered with <c>Name</c> leading.
    /// </summary>
    public List<List<KeyValuePair<string, string>>> Constants { get; } = [];

    /// <summary>
    /// Gets visual definition declarations, their root attributes, and child visual states.
    /// </summary>
    public List<(string Name, List<KeyValuePair<string, string>> Attributes, List<(string State, List<KeyValuePair<string, string>> Attributes)> States)> VisualDefinitions { get; } = [];

    public List<KeyValuePair<string, string>> CustomElements { get; } = [];

    public FuzzPrefab Clone()
    {
        var clone = new FuzzPrefab(Name, Root.Clone());
        clone.Parameters.AddRange(Parameters);
        clone.Constants.AddRange(Constants.Select(x => x.ToList()));
        clone.VisualDefinitions.AddRange(VisualDefinitions.Select(x => (x.Name, x.Attributes.ToList(), x.States.Select(s => (s.State, s.Attributes.ToList())).ToList())));
        clone.CustomElements.AddRange(CustomElements);
        return clone;
    }

    public string ToXml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Prefab>");
        if (Parameters.Count > 0)
        {
            sb.AppendLine("  <Parameters>");
            foreach (var (name, value) in Parameters)
                sb.AppendLine($"    <Parameter Name=\"{Escape(name)}\" DefaultValue=\"{Escape(value)}\" />");
            sb.AppendLine("  </Parameters>");
        }
        if (Constants.Count > 0)
        {
            sb.AppendLine("  <Constants>");
            foreach (var constant in Constants)
                sb.AppendLine($"    <Constant{Attributes(constant)} />");
            sb.AppendLine("  </Constants>");
        }
        if (VisualDefinitions.Count > 0)
        {
            sb.AppendLine("  <VisualDefinitions>");
            foreach (var (name, attributes, states) in VisualDefinitions)
            {
                sb.AppendLine($"    <VisualDefinition Name=\"{Escape(name)}\"{Attributes(attributes)}>");
                foreach (var (state, stateAttributes) in states)
                    sb.AppendLine($"      <VisualState State=\"{Escape(state)}\"{Attributes(stateAttributes)} />");
                sb.AppendLine("    </VisualDefinition>");
            }
            sb.AppendLine("  </VisualDefinitions>");
        }
        if (CustomElements.Count > 0)
        {
            sb.AppendLine("  <CustomElements>");
            foreach (var (name, xml) in CustomElements)
                sb.AppendLine($"    <CustomElement Name=\"{Escape(name)}\">{xml}</CustomElement>");
            sb.AppendLine("  </CustomElements>");
        }
        sb.AppendLine("  <Window>");
        Render(sb, Root, "    ");
        sb.AppendLine("  </Window>");
        sb.AppendLine("</Prefab>");
        return sb.ToString();
    }

    private static void Render(StringBuilder sb, FuzzWidget widget, string indent)
    {
        var inner = widget.Children.Count > 0 || widget.ItemTemplate is not null || widget.LogicalChildrenLocation;
        sb.Append($"{indent}<{widget.Type}{Attributes(widget.Attributes)}");
        if (!inner)
        {
            sb.AppendLine(" />");
            return;
        }
        sb.AppendLine(">");
        if (widget.LogicalChildrenLocation)
            sb.AppendLine($"{indent}  <LogicalChildrenLocation />");
        if (widget.ItemTemplate is not null)
        {
            sb.AppendLine($"{indent}  <ItemTemplate>");
            Render(sb, widget.ItemTemplate, indent + "    ");
            sb.AppendLine($"{indent}  </ItemTemplate>");
        }
        if (widget.Children.Count > 0)
        {
            sb.AppendLine($"{indent}  <Children>");
            foreach (var child in widget.Children)
                Render(sb, child, indent + "    ");
            sb.AppendLine($"{indent}  </Children>");
        }
        sb.AppendLine($"{indent}</{widget.Type}>");
    }

    private static string Attributes(IEnumerable<KeyValuePair<string, string>> attributes) =>
        string.Concat(attributes.Select(x => $" {x.Key}=\"{Escape(x.Value)}\""));

    /// <summary>
    /// Escapes XML attribute values, preserving newline and control characters as numeric character references.
    /// </summary>
    private static string Escape(string value) => value
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;")
        .Replace("\r", "&#13;").Replace("\n", "&#10;").Replace("\t", "&#9;");
}

/// <summary>
/// Encapsulates a complete fuzzing scenario consisting of a root movie, component prefabs, and the generating random seed.
/// </summary>
internal sealed class FuzzCase
{
    public FuzzCase(int seed, FuzzPrefab movie, List<FuzzPrefab> parts)
    {
        Seed = seed;
        Movie = movie;
        Parts = parts;
    }

    public int Seed { get; }
    public FuzzPrefab Movie { get; }
    public List<FuzzPrefab> Parts { get; }

    public IEnumerable<FuzzPrefab> Prefabs => new[] { Movie }.Concat(Parts);

    public FuzzCase Clone() => new(Seed, Movie.Clone(), [.. Parts.Select(x => x.Clone())]);

    public override string ToString() => string.Join(Environment.NewLine, Prefabs.Select(x => $"--- {x.Name}.xml{Environment.NewLine}{x.ToXml()}"));

    /// <summary>
    /// Generates minimized variations of the test case by progressively removing component parts, widgets, templates, declarations, and attributes.
    /// </summary>
    public IEnumerable<FuzzCase> Shrinks()
    {
        for (var i = 0; i < Parts.Count; i++)
        {
            var copy = Clone();
            var name = copy.Parts[i].Name;
            copy.Parts.RemoveAt(i);
            // Replaces the dropped component usage with a plain Widget while preserving attributes.
            foreach (var widget in copy.Prefabs.SelectMany(x => x.Root.Descendants()).Where(x => x.Type == name))
                widget.Type = "Widget";
            yield return copy;
        }

        for (var p = 0; p < Prefabs.Count(); p++)
        {
            var widgets = Prefabs.ElementAt(p).Root.Descendants().ToList();
            for (var w = 0; w < widgets.Count; w++)
            {
                var widget = widgets[w];
                for (var c = 0; c < widget.Children.Count; c++)
                {
                    yield return Edit(p, w, x => x.Children.RemoveAt(c));
                    yield return Edit(p, w, x =>
                    {
                        var removed = x.Children[c];
                        x.Children.RemoveAt(c);
                        x.Children.InsertRange(c, removed.Children);
                    });
                }
                if (widget.ItemTemplate is not null)
                    yield return Edit(p, w, x => x.ItemTemplate = null);
                if (widget.LogicalChildrenLocation)
                    yield return Edit(p, w, x => x.LogicalChildrenLocation = false);
            }
        }

        for (var p = 0; p < Prefabs.Count(); p++)
        {
            var prefab = Prefabs.ElementAt(p);
            for (var i = 0; i < prefab.Parameters.Count; i++)
                yield return EditPrefab(p, x => x.Parameters.RemoveAt(i));
            for (var i = 0; i < prefab.Constants.Count; i++)
                yield return EditPrefab(p, x => x.Constants.RemoveAt(i));
            for (var i = 0; i < prefab.VisualDefinitions.Count; i++)
                yield return EditPrefab(p, x => x.VisualDefinitions.RemoveAt(i));
            for (var i = 0; i < prefab.CustomElements.Count; i++)
                yield return EditPrefab(p, x => x.CustomElements.RemoveAt(i));
        }

        for (var p = 0; p < Prefabs.Count(); p++)
        {
            var widgets = Prefabs.ElementAt(p).Root.Descendants().ToList();
            for (var w = 0; w < widgets.Count; w++)
            {
                for (var a = 0; a < widgets[w].Attributes.Count; a++)
                    yield return Edit(p, w, x => x.Attributes.RemoveAt(a));
            }
        }
    }

    private FuzzCase EditPrefab(int prefab, Action<FuzzPrefab> edit)
    {
        var copy = Clone();
        edit(copy.Prefabs.ElementAt(prefab));
        return copy;
    }

    private FuzzCase Edit(int prefab, int widget, Action<FuzzWidget> edit)
    {
        var copy = Clone();
        edit(copy.Prefabs.ElementAt(prefab).Root.Descendants().ElementAt(widget));
        return copy;
    }

    /// <summary>
    /// Computes the structural complexity score of the test case across all prefabs, widgets, and declarations.
    /// </summary>
    public int Size => Prefabs.Sum(x => x.Root.Descendants().Sum(w => 1 + w.Attributes.Count) + x.Parameters.Count + x.Constants.Count + x.VisualDefinitions.Count + x.CustomElements.Count);
}
