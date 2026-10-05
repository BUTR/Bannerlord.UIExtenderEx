using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;

using NSubstitute;

using NUnit.Framework;

using System;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Layout;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Verifies code generation and runtime evaluation for attributes whose dotted property paths traverse interface or base class types.
/// <para>
/// For properties such as <c>Widget.LayoutImp</c> declared as <see cref="ILayout"/>, downstream properties like <c>LayoutMethod</c>
/// exist on concrete implementations (<see cref="StackLayout"/>). Unlike static member resolution which cannot traverse declared base types,
/// the generator emits runtime calls to <c>SetWidgetAttributeFromString</c> to mirror XML loader object inspection and avoid dropping attributes.
/// </para>
/// </summary>
public class RuntimeResolvedAttributeTests
{
    private const string Movie = "RuntimeResolvedMovie";

    private const string MoviePrefab = """
<Prefab>
  <Window>
    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" LayoutImp.LayoutMethod="VerticalTopToBottom" />
  </Window>
</Prefab>
""";

    private PrefabWorkspace? _workspace;
    private SpriteData? _spriteData;
    private BrushFactory? _brushFactory;


    [SetUp]
    public void SetUp()
    {
        _workspace = new PrefabWorkspace((Movie, MoviePrefab));
        _spriteData = new SpriteData("RuntimeResolvedAttributeTests");
        _brushFactory = new BrushFactory(_workspace.ResourceDepot, "Brushes", _spriteData, new FontFactory(_workspace.ResourceDepot));
    }

    [TearDown]
    public void TearDown() => _workspace?.Dispose();

    [Test]
    public void DottedPathThroughABaseTypedProperty_IsEmittedRatherThanDropped()
    {
        var code = string.Join("\n", _workspace!.Generate(Movie, typeof(CodegenTestVM)).Select(x => x.Content));

        Assert.That(code, Does.Contain("LayoutImp.LayoutMethod"), "the attribute was dropped");
        // Emits SetWidgetAttributeFromString because SetWidgetAttribute does not support enum conversion.
        Assert.That(code, Does.Contain("SetWidgetAttributeFromString("));
        Assert.That(code, Does.Contain("VerticalTopToBottom"));
    }

    /// <summary>
    /// Verifies that the emitted runtime attribute call successfully assigns concrete enum values without throwing during execution.
    /// </summary>
    [Test]
    public void TheEmittedCall_ActuallySetsTheLayoutMethod()
    {
        var compiler = new RoslynCompiler();

        var references = PrefabReferenceSet.CollectPaths(_workspace!.WidgetFactory, typeof(CodegenTestVM));
        var sources = _workspace.Generate(Movie, typeof(CodegenTestVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = compiler.Compile(CompiledPrefabManager.GetAssemblyName(Movie, typeof(CodegenTestVM), "runtimeresolved00"), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));

        var environment = new GameCompiledPrefabEnvironment();
        var assembly = environment.LoadAssembly(result.Assembly!);
        // Registers generated widget classes with WidgetInfo as required by base Widget constructors.
        Assert.That(environment.CreateCreator(assembly), Is.Not.Null);
        var rootType = assembly.GetTypes().First(x => x.Name.StartsWith(Movie + "__", StringComparison.Ordinal));

        var widget = (Widget) Activator.CreateInstance(rootType, CreateUIContext())!;
        foreach (var name in new[] { "CreateWidgets", "SetIds", "SetAttributes" })
            Assert.That(() => rootType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(widget, []),
                Throws.Nothing, name + " threw");

        Assert.That(widget.LayoutImp, Is.InstanceOf<StackLayout>());
        Assert.That(((StackLayout) widget.LayoutImp).LayoutMethod, Is.EqualTo(LayoutMethod.VerticalTopToBottom));
    }

    /// <summary>
    /// Verifies that statically unresolvable binding paths (such as undeclared mixin properties or modified ViewModel members)
    /// fall back to dynamic member resolution (<see cref="DynamicMember.GetChild"/>) matching XML loader behavior.
    /// </summary>
    [Test]
    public void AnUnresolvableBindingPath_IsGeneratedAgainstTheInstance()
    {
        using var workspace = new PrefabWorkspace(("UnresolvableMovie", """
<Prefab>
  <Window><Widget><Children>
    <ListPanel DataSource="{Items}">
      <ItemTemplate>
        <Widget WidthSizePolicy="StretchToParent" />
      </ItemTemplate>
    </ListPanel>
  </Children></Widget></Window>
</Prefab>
"""));

        // Tests an unresolvable path where Items is not registered on the static ViewModel type.
        var code = string.Join(Environment.NewLine, workspace.Generate("UnresolvableMovie", typeof(CodegenTestVM)).Select(x => x.Content));

        Assert.That(code, Does.Contain("private global::TaleWorlds.Library.IMBBindingList _datasource_Root_Items;"));
        Assert.That(code, Does.Contain("DynamicMember.GetChild(_datasource_Root, \"Items\") as global::TaleWorlds.Library.IMBBindingList"));
    }

    /// <summary>
    /// Verifies that unrecognized direct properties on known widget types are pruned rather than delegated to runtime resolution.
    /// </summary>
    [Test]
    public void UnknownPlainAttribute_IsStillDropped()
    {
        using var workspace = new PrefabWorkspace(("UnknownPlainMovie", """
<Prefab>
  <Window>
    <Widget ThisPropertyDoesNotExist="17" />
  </Window>
</Prefab>
"""));

        var code = string.Join("\n", workspace.Generate("UnknownPlainMovie", typeof(CodegenTestVM)).Select(x => x.Content));

        Assert.That(code, Does.Not.Contain("ThisPropertyDoesNotExist"));
    }

    private UIContext CreateUIContext()
    {
        var platform = Substitute.For<ITwoDimensionPlatform>();
        platform.ReferenceHeight.Returns(1080f);
        platform.ReferenceWidth.Returns(1920f);
        var twoDimension = new TwoDimensionContext(platform, Substitute.For<ITwoDimensionResourceContext>(), _workspace!.ResourceDepot);
        TestInput.EnsureInitialized();
        var context = new UIContext(twoDimension, Substitute.For<TaleWorlds.InputSystem.IInputContext>(), _spriteData!, new FontFactory(_workspace.ResourceDepot), _brushFactory!);
        // Initializes the EventManager required to invalidate layouts when widget properties change.
        context.Initialize();
        return context;
    }
}
