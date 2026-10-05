using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Provides an end-to-end test fixture that compiles, loads, and instantiates compiled prefabs against live <see cref="UIContext"/>
/// and <see cref="ViewModel"/> instances.
/// <para>
/// Validates runtime binding behavior beyond source code emission assertions, verifying by-name and typed property bindings,
/// event commands, and lifecycle methods directly on instantiated widget trees.
/// </para>
/// </summary>
public sealed class CompiledMovie
{
    private static int _salt;

    private readonly Type _rootType;

    private CompiledMovie(Type rootType, Widget root)
    {
        _rootType = rootType;
        Root = root;
    }

    public Widget Root { get; }

    public static CompiledMovie Build(PrefabWorkspace workspace, string movieName, Type viewModelType, TestUIContext ui)
    {
        var compiler = new RoslynCompiler();

        var environment = new GameCompiledPrefabEnvironment();
        var references = PrefabReferenceSet.CollectPaths(workspace.WidgetFactory, viewModelType);
        var sources = workspace.Generate(movieName, viewModelType).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var name = CompiledPrefabManager.GetAssemblyName(movieName, viewModelType, $"harness{System.Threading.Interlocked.Increment(ref _salt):D10}");
        var result = compiler.Compile(name, sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));

        var assembly = environment.LoadAssembly(result.Assembly!);
        // Register assembly widget types with WidgetInfo prior to instantiating Widget constructors.
        Assert.That(environment.CreateCreator(assembly), Is.Not.Null);
        // Resolve the root widget class using sanitized movie identifier naming.
        var rootType = assembly.GetTypes().First(x => x.Name.StartsWith(GeneratedNaming.GetUsableName(movieName) + "__", StringComparison.Ordinal));

        var root = (Widget) Activator.CreateInstance(rootType, ui.Context)!;
        foreach (var step in new[] { "CreateWidgets", "SetIds", "SetAttributes" })
            Invoke(rootType, root, step);

        return new CompiledMovie(rootType, root);
    }

    public void SetDataSource(ViewModel? dataSource) => Invoke(_rootType, Root, "SetDataSource", dataSource);

    public void DestroyDataSource() => Invoke(_rootType, Root, "DestroyDataSource");

    /// <summary>Finds the first widget matching the specified ID using depth-first tree traversal.</summary>
    public Widget ById(string id) => FindById(Root, id) ?? throw new InvalidOperationException($"No widget with Id '{id}' in the compiled movie.");

    public Widget? FindById(string id) => FindById(Root, id);

    /// <summary>Collects all widgets matching the specified ID in tree order, including template-instantiated items.</summary>
    public List<Widget> AllById(string id)
    {
        var found = new List<Widget>();
        Collect(Root, id, found);
        return found;
    }

    private static Widget? FindById(Widget widget, string id)
    {
        if (widget.Id == id)
            return widget;
        for (var i = 0; i < widget.ChildCount; i++)
        {
            if (FindById(widget.GetChild(i), id) is { } found)
                return found;
        }
        return null;
    }

    private static void Collect(Widget widget, string id, List<Widget> found)
    {
        if (widget.Id == id)
            found.Add(widget);
        for (var i = 0; i < widget.ChildCount; i++)
            Collect(widget.GetChild(i), id, found);
    }

    /// <summary>
    /// Invokes the protected <c>Widget.EventFired</c> method to simulate UI event dispatch to bound commands.
    /// </summary>
    public static void FireEvent(Widget widget, string eventName, params object[] arguments)
    {
        var method = typeof(Widget).GetMethod("EventFired", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Widget.EventFired is gone.");
        method.Invoke(widget, [eventName, arguments]);
    }

    private static void Invoke(Type type, object target, string methodName, params object?[] arguments)
    {
        var method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(x => x.Name == methodName && x.GetParameters().Length == arguments.Length)
            ?? throw new InvalidOperationException($"The generated class has no '{methodName}' taking {arguments.Length} arguments.");
        try
        {
            method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is { } inner)
        {
            throw new InvalidOperationException($"'{methodName}' threw: {inner}", inner);
        }
    }
}