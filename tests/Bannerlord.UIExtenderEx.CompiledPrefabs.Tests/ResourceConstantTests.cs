using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Widgets;
using Bannerlord.UIExtenderEx.ResourceManager;

using NSubstitute;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests code generation and runtime evaluation for constants dependent on sprite and brush-layer dimensions.
/// <para>
/// While static constants are baked into generated source code during compilation, resource-dependent constants
/// (<c>SpriteWidth</c>, <c>SpriteHeight</c>, <c>BrushLayerWidth</c>, and <c>BrushLayerHeight</c>) can change dynamically
/// when texture packs or brush definitions are modified at runtime without XML alterations. Code generation preserves
/// runtime resolution for these constants against the active UI context.
/// </para>
/// </summary>
public class ResourceConstantTests
{
    private const string Movie = "ResourceConstantMovie";
    private const string SpriteName = "ResourceConstantSprite";
    private const string BrushName = "ResourceConstantBrush";

    private const int SpriteWidth = 100;
    private const int LayerSpriteWidth = 200;
    private const float ExtendLeft = 5f;
    private const float ExtendRight = 7f;

    /// <summary>
    /// Prefab XML template covering direct dimensions, additive arithmetic, conditional branches,
    /// parameter-referenced brushes, and plain numeric literals.
    /// </summary>
    private const string MoviePrefab = $@"
<Prefab>
  <Parameters>
    <Parameter Name=""PanelBrush"" DefaultValue=""{BrushName}"" />
  </Parameters>
  <Constants>
    <Constant Name=""IconWidth"" SpriteName=""{SpriteName}"" SpriteValueType=""Width"" />
    <Constant Name=""IconWidthPlusMargin"" Value=""!IconWidth"" Additive=""10"" />
    <Constant Name=""PanelWidth"" BrushName=""*PanelBrush"" BrushLayer=""Default"" BrushValueType=""Width"" />
    <Constant Name=""Branch"" BooleanCheck=""true"" OnTrue=""!IconWidthPlusMargin"" OnFalse=""1"" />
    <Constant Name=""Plain"" Value=""42"" />
  </Constants>
  <VisualDefinitions>
    <VisualDefinition Name=""Resize"">
      <VisualState State=""Default"" SuggestedWidth=""!IconWidth"" SuggestedHeight=""!Plain"" />
    </VisualDefinition>
  </VisualDefinitions>
  <Window>
    <Widget SuggestedWidth=""!IconWidth"" SuggestedHeight=""!IconWidthPlusMargin"" MarginLeft=""!PanelWidth"" MarginTop=""!Branch"" MarginRight=""!Plain"" />
  </Window>
</Prefab>";

    /// <summary>
    /// Test sprite implementation providing explicit dimensions without rendering dependencies.
    /// </summary>
    private sealed class TestSprite : Sprite
    {
        public TestSprite(string name, int width, int height) : base(name, width, height, SpriteNinePatchParameters.Empty) { }
        public override Texture Texture => null!;
        public override Vec2 GetMinUvs() => Vec2.Zero;
        public override Vec2 GetMaxUvs() => Vec2.One;
    }

    private PrefabWorkspace? _workspace;
    private UIExtender? _extender;
    private SpriteData? _spriteData;
    private BrushFactory? _brushFactory;


    [SetUp]
    public void SetUp()
    {
        _extender = UIExtender.Create("TestModule.CompiledPrefabs.ResourceConstants");
        _workspace = new PrefabWorkspace((Movie, MoviePrefab));

        _spriteData = new SpriteData("ResourceConstantTests");
        _spriteData.Sprites[SpriteName] = new TestSprite(SpriteName, SpriteWidth, 50);
        _spriteData.Sprites[BrushName + ".Layer"] = new TestSprite(BrushName + ".Layer", LayerSpriteWidth, 80);

        var brush = new Brush { Name = BrushName };
        // Populates the existing "Default" brush layer created by Brush instantiation.
        var layer = brush.GetLayer("Default");
        if (layer is null)
        {
            layer = new BrushLayer { Name = "Default" };
            brush.AddLayer(layer);
        }
        layer.Sprite = _spriteData.Sprites[BrushName + ".Layer"];
        layer.ExtendLeft = ExtendLeft;
        layer.ExtendRight = ExtendRight;
        // Registers the brush so that the BrushFactory patch serves it globally within the test process.
        BrushFactoryManager.Register([brush]);

        _brushFactory = new BrushFactory(_workspace.ResourceDepot, "Brushes", _spriteData, new FontFactory(_workspace.ResourceDepot));
        PrefabFingerprint.ClearCache();
    }

    [TearDown]
    public void TearDown()
    {
        _extender?.Deregister();
        _workspace?.Dispose();
        PrefabFingerprint.ClearCache();
    }

    private string Generate() => string.Join("\n", GenerateSources().Select(x => x.Content));

    private List<GeneratedSource> GenerateSources() => GenerateSources(_workspace!, Movie);

    private List<GeneratedSource> GenerateSources(PrefabWorkspace workspace, string movieName)
    {
        using var lease = WidgetFactoryLookup.PrefabLease.Begin(workspace.WidgetFactory);
        var context = new PrefabCodeGenerator("Bannerlord.UIExtenderEx.Tests.Generated", workspace.WidgetFactory, _spriteData!, _brushFactory!);
        context.AddMovie(movieName, typeof(CodegenTestVM).FullName, typeof(CodegenTestVM));
        return [.. context.GenerateInMemory().Select(x => new GeneratedSource(x.Key, x.Value))];
    }

    // --- Resolution and literal emission tests -----------------------------------------------------------------------

    [Test]
    public void ASpriteDimension_IsResolvedWhenTheWidgetIsCreated()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("uiExtenderExResourceValue = this.UIExtenderEx_ResolveResourceValue(@\"!IconWidth\");"));
        Assert.That(code, Does.Contain(
            "this.SuggestedWidth = global::System.Convert.ToSingle(uiExtenderExResourceValue, global::System.Globalization.CultureInfo.InvariantCulture);"));
        Assert.That(code, Does.Not.Contain($"this.SuggestedWidth = {SpriteWidth}f;"), "the size a texture pack can change must not be baked in");
    }

    [Test]
    public void ArithmeticOnASpriteDimension_IsResolvedToo()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("uiExtenderExResourceValue = this.UIExtenderEx_ResolveResourceValue(@\"!IconWidthPlusMargin\");"));
        Assert.That(code, Does.Contain("this.SuggestedHeight = global::System.Convert.ToSingle(uiExtenderExResourceValue"));
    }

    [Test]
    public void AConditionalBranchReachingASpriteDimension_IsResolvedToo()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("uiExtenderExResourceValue = this.UIExtenderEx_ResolveResourceValue(@\"!Branch\");"));
        Assert.That(code, Does.Contain("this.MarginTop = global::System.Convert.ToSingle(uiExtenderExResourceValue"));
    }

    [Test]
    public void ABrushLayerDimension_ReachedThroughAParameter_IsResolvedToo()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("uiExtenderExResourceValue = this.UIExtenderEx_ResolveResourceValue(@\"!PanelWidth\");"));
        Assert.That(code, Does.Contain("this.MarginLeft = global::System.Convert.ToSingle(uiExtenderExResourceValue"));
        Assert.That(code, Does.Contain($"defaultParameters.Add(@\"PanelBrush\", @\"{BrushName}\");"), "the brush name is only reachable through the parameter values");
    }

    [Test]
    public void AConstantThatReadsNoResource_KeepsItsLiteral()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("//From constant Plain:42"));
        Assert.That(code, Does.Contain("this.MarginRight = 42f;"));
    }

    [Test]
    public void AVisualState_ResolvesOnlyItsResourceDependentValues()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("visualState.SuggestedWidth = global::System.Convert.ToSingle(uiExtenderExResourceValue"));
        Assert.That(code, Does.Contain("visualState.SuggestedHeight = 42f;"));
    }

    [Test]
    public void TheReconstructedDefinitions_CarryWhatTheGamesResolverReads()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("constant.Type = global::TaleWorlds.GauntletUI.PrefabSystem.ConstantDefinitionType.SpriteWidth;"));
        Assert.That(code, Does.Contain($"constant.SpriteName = @\"{SpriteName}\";"));
        Assert.That(code, Does.Contain("constant.Type = global::TaleWorlds.GauntletUI.PrefabSystem.ConstantDefinitionType.BrushLayerWidth;"));
        Assert.That(code, Does.Contain("constant.BrushName = @\"*PanelBrush\";"));
        Assert.That(code, Does.Contain("constant.LayerName = @\"Default\";"));
        Assert.That(code, Does.Contain("constant.Additive = @\"10\";"));
        // Preserves all constants because GetValue resolves references across the full dictionary.
        Assert.That(code, Does.Contain("constants.Add(@\"Plain\", constant);"));
        Assert.That(code, Does.Contain("constant.MultiplyResult = 1f;"), "written the invariant way, whatever culture generated it");
    }

    /// <summary>
    /// Verifies that generated resource resolution mirrors XML loader error handling by suppressing assignments
    /// for empty values and trapping conversion exceptions via diagnostic assertions.
    /// </summary>
    [Test]
    public void AResolvedValue_IsGuardedTheWayTheLoaderGuardsIts()
    {
        var code = Generate();

        Assert.That(code, Does.Contain("if (!string.IsNullOrEmpty(uiExtenderExResourceValue))"));
        Assert.That(code, Does.Contain("catch (global::System.Exception uiExtenderExResourceFailure)"));
        Assert.That(code, Does.Contain("global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.LoaderAsserts.AttributeNotSet("),
            "reported through the loader's own channel, in its words");
        Assert.That(code, Does.Contain(", uiExtenderExResourceValue, uiExtenderExResourceFailure.Message);"),
            "quoting the value resolved and what was caught, as the loader's catch does");
        Assert.That(System.Text.RegularExpressions.Regex.Matches(code, "UIExtenderEx_ResolveResourceValue\\(@\"!IconWidth\"\\)").Count,
            Is.EqualTo(2), "resolved once per use - the widget attribute and the visual state - not once per read");
    }

    [Test]
    public void APrefabWithoutResourceConstants_GetsNoResolver()
    {
        using var plain = new PrefabWorkspace(("ResourceConstantPlainMovie", PrefabWorkspace.PlainPrefab));

        var code = string.Join("\n", plain.Generate("ResourceConstantPlainMovie", typeof(CodegenTestVM)).Select(x => x.Content));

        Assert.That(code, Does.Not.Contain("UIExtenderEx_ResolveResourceValue"));
        Assert.That(code, Does.Not.Contain("_uiExtenderExConstantDefinitions"));
    }

    // --- XML loader parity tests --------------------------------------------------------------------------------------

    [Test]
    public void TheResolvedValues_AreWhatTheXmlLoaderWouldHaveSet()
    {
        var widget = BuildCompiledWidget(out _);

        Assert.That(widget.SuggestedWidth, Is.EqualTo(XmlValueOf("IconWidth")));
        Assert.That(widget.SuggestedHeight, Is.EqualTo(XmlValueOf("IconWidthPlusMargin")));
        Assert.That(widget.MarginLeft, Is.EqualTo(XmlValueOf("PanelWidth")));
        Assert.That(widget.MarginTop, Is.EqualTo(XmlValueOf("Branch")));
        Assert.That(widget.MarginRight, Is.EqualTo(XmlValueOf("Plain")));

        // Verifies dimensions match underlying sprite and layer properties.
        Assert.That(widget.SuggestedWidth, Is.EqualTo((float) SpriteWidth));
        Assert.That(widget.SuggestedHeight, Is.EqualTo((float) SpriteWidth + 10f));
        Assert.That(widget.MarginLeft, Is.EqualTo((float) (int) (LayerSpriteWidth - ExtendLeft - ExtendRight)));
    }

    /// <summary>
    /// Verifies that runtime alterations to sprite dimensions propagate to newly created widgets without recompilation.
    /// </summary>
    [Test]
    public void ResizingASprite_ChangesNewlyCreatedWidgets_WithoutRecompiling()
    {
        var first = BuildCompiledWidget(out var rootType);
        Assert.That(first.SuggestedWidth, Is.EqualTo((float) SpriteWidth));

        _spriteData!.Sprites[SpriteName] = new TestSprite(SpriteName, 130, 50);
        var second = Instantiate(rootType);

        Assert.That(second.SuggestedWidth, Is.EqualTo(130f));
        Assert.That(second.SuggestedHeight, Is.EqualTo(140f), "and everything computed from it");
        Assert.That(second.SuggestedWidth, Is.EqualTo(XmlValueOf("IconWidth")), "the same as the XML loader would set now");
        Assert.That(first.SuggestedWidth, Is.EqualTo((float) SpriteWidth), "a widget already on screen keeps what it was built with");
    }

    [Test]
    public void ChangingABrushLayersExtends_ChangesNewlyCreatedWidgets()
    {
        var first = BuildCompiledWidget(out var rootType);
        var before = first.MarginLeft;

        _brushFactory!.GetBrush(BrushName).GetLayer("Default").ExtendRight = 57f;
        var second = Instantiate(rootType);

        Assert.That(second.MarginLeft, Is.Not.EqualTo(before));
        Assert.That(second.MarginLeft, Is.EqualTo(XmlValueOf("PanelWidth")));
    }

    /// <summary>
    /// Verifies that removing a sprite dependency triggers a <see cref="FormatException"/> during arithmetic evaluation,
    /// matching XML loader failure semantics when converting empty strings.
    /// </summary>
    [Test]
    public void RemovingASprite_ThrowsAsTheXmlLoaderDoes()
    {
        var first = BuildCompiledWidget(out var rootType);
        Assert.That(first.SuggestedWidth, Is.EqualTo((float) SpriteWidth), "test premise: it was set while the sprite was there");

        _spriteData!.Sprites.Remove(SpriteName);

        Assert.That(XmlStringOf("IconWidth"), Is.Empty, "the dimension alone comes back empty and is not assigned");
        Assert.That(() => XmlStringOf("IconWidthPlusMargin"), Throws.InstanceOf<FormatException>(), "test premise: XML throws on the arithmetic");
        var thrown = Assert.Catch(() => Instantiate(rootType));
        Assert.That(thrown!.GetBaseException(), Is.InstanceOf<FormatException>());
        Assert.That(first.SuggestedWidth, Is.EqualTo((float) SpriteWidth), "the widget built earlier is untouched");
    }

    private const string MissingMovie = "ResourceConstantMissingMovie";

    /// <summary>
    /// Prefab XML snippet referencing missing sprites, brushes, and layers.
    /// </summary>
    private const string MissingPrefab = $@"
<Prefab>
  <Constants>
    <Constant Name=""NoSuchSprite"" SpriteName=""ResourceConstantNoSuchSprite"" SpriteValueType=""Width"" />
    <Constant Name=""NoSuchBrush"" BrushName=""ResourceConstantNoSuchBrush"" BrushLayer=""Default"" BrushValueType=""Width"" />
    <Constant Name=""NoSuchLayer"" BrushName=""{BrushName}"" BrushLayer=""ResourceConstantNoSuchLayer"" BrushValueType=""Height"" />
  </Constants>
  <Window>
    <Widget SuggestedWidth=""!NoSuchSprite"" SuggestedHeight=""!NoSuchBrush"" MarginTop=""!NoSuchLayer"" />
  </Window>
</Prefab>";

    /// <summary>
    /// Verifies that missing sprites, brushes, or layers match XML loader parity by throwing or leaving attributes unset.
    /// </summary>
    [TestCase("SuggestedWidth", "NoSuchSprite")]
    [TestCase("SuggestedHeight", "NoSuchBrush")]
    [TestCase("MarginTop", "NoSuchLayer")]
    public void AMissingSpriteBrushOrLayer_BehavesAsTheXmlLoader(string attribute, string constant)
    {
        var movie = MissingMovie + constant;
        using var workspace = new PrefabWorkspace((movie, MissingPrefab.Replace("<Widget SuggestedWidth=\"!NoSuchSprite\" SuggestedHeight=\"!NoSuchBrush\" MarginTop=\"!NoSuchLayer\" />", $"<Widget {attribute}=\"!{constant}\" />")));

        Exception? xml = null;
        var xmlValue = "";
        try
        {
            xmlValue = XmlStringOf(workspace, movie, constant);
        }
        catch (Exception e)
        {
            xml = e;
        }

        Widget? widget = null;
        Exception? compiled = null;
        try
        {
            widget = BuildCompiledWidget(workspace, movie, out _);
        }
        catch (Exception e)
        {
            compiled = e.GetBaseException();
        }

        if (xml is not null)
        {
            Assert.That(compiled, Is.InstanceOf(xml.GetType()), $"XML throws {xml.GetType().Name}");
            return;
        }
        Assert.That(compiled, Is.Null, $"XML resolves it to '{xmlValue}'");
        Assert.That(xmlValue, Is.Empty, "test premise: nothing to resolve against");
        Assert.That(typeof(Widget).GetProperty(attribute)!.GetValue(widget), Is.EqualTo(0f), "an empty value is not assigned");
    }

    private const string DottedMovie = "ResourceConstantDottedMovie";

    /// <summary>
    /// Verifies that resource constants applied to untyped property paths (such as <see cref="object"/> holders)
    /// route through <c>SetWidgetAttributeFromString</c> matching XML loader behavior.
    /// </summary>
    [Test]
    public void AResourceConstantOnAPathOnlyTheLiveObjectHas_ReachesItAsItDoesInXml()
    {
        using var workspace = new PrefabWorkspace((DottedMovie, $@"
<Prefab>
  <Constants>
    <Constant Name=""IconWidth"" SpriteName=""{SpriteName}"" SpriteValueType=""Width"" />
  </Constants>
  <Window>
    <LooseHolderWidget Holder.Value=""!IconWidth"" />
  </Window>
</Prefab>"));

        var code = string.Join("\n", GenerateSources(workspace, DottedMovie).Select(x => x.Content));
        var widget = (LooseHolderWidget) BuildCompiledWidget(workspace, DottedMovie, out _);

        Assert.That(code, Does.Contain("TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions.SetWidgetAttributeFromString(this, @\"Holder.Value\", uiExtenderExResourceValue, "));
        Assert.That(((LooseHolder) widget.Holder).Value, Is.EqualTo((float) SpriteWidth));
    }

    /// <summary>
    /// Evaluates the constant using the XML loader against the current resource state.
    /// </summary>
    private string XmlStringOf(string constantName) => XmlStringOf(_workspace!, Movie, constantName);

    private string XmlStringOf(PrefabWorkspace workspace, string movie, string constantName)
    {
        var prefab = workspace.Load(movie);
        try
        {
            return prefab.Constants[constantName].GetValue(_brushFactory!, _spriteData!, prefab.Constants, new Dictionary<string, WidgetAttributeTemplate>(), prefab.Parameters);
        }
        finally
        {
            workspace.WidgetFactory.OnUnload(movie);
        }
    }

    /// <summary>
    /// Computes the numeric value the XML loader applies to a widget property for the specified constant.
    /// </summary>
    private float XmlValueOf(string constantName)
    {
        var prefab = _workspace!.Load(Movie);
        try
        {
            var parameters = new Dictionary<string, WidgetAttributeTemplate>();
            var value = prefab.Constants[constantName].GetValue(_brushFactory!, _spriteData!, prefab.Constants, parameters, prefab.Parameters);
            return Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            _workspace.WidgetFactory.OnUnload(Movie);
        }
    }

    /// <summary>
    /// Verifies that generated resource resolver code compiles successfully using the Roslyn compiler backend.
    /// </summary>
    [TestCase(typeof(RoslynCompiler))]
    public void TheGeneratedResolver_CompilesWithEitherBackend(Type compilerType)
    {
        var compiler = (ICSharpCompiler) Activator.CreateInstance(compilerType)!;

        var references = PrefabReferenceSet.CollectPaths(_workspace!.WidgetFactory, typeof(CodegenTestVM));
        var sources = GenerateSources().Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        Assert.That(sources.Any(x => x.Content.Contains("UIExtenderEx_ResolveResourceValue")), Is.True, "test premise: the resolver is in there");

        var result = compiler.Compile(CompiledPrefabManager.GetAssemblyName(Movie, typeof(CodegenTestVM), "backend" + compilerType.Name), sources, references);

        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
    }

    private Widget BuildCompiledWidget(out Type rootType) => BuildCompiledWidget(_workspace!, Movie, out rootType);

    private Widget BuildCompiledWidget(PrefabWorkspace workspace, string movieName, out Type rootType)
    {
        var compiler = new RoslynCompiler();

        var references = PrefabReferenceSet.CollectPaths(workspace.WidgetFactory, typeof(CodegenTestVM));
        var sources = GenerateSources(workspace, movieName).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        var result = compiler.Compile(CompiledPrefabManager.GetAssemblyName(movieName, typeof(CodegenTestVM), "resourceconstant00"), sources, references);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));

        var environment = new GameCompiledPrefabEnvironment();
        var assembly = environment.LoadAssembly(result.Assembly!);
        var creator = environment.CreateCreator(assembly);
        Assert.That(creator, Is.Not.Null, "the compiled assembly carries a creator");

        var context = new GeneratedPrefabContext();
        creator!(context);
        rootType = assembly.GetTypes().First(x => x.Name.StartsWith(movieName + "__", StringComparison.Ordinal));
        return Instantiate(rootType);
    }

    /// <summary>
    /// Instantiates and initializes a generated widget by invoking lifecycle methods (<c>CreateWidgets</c>, <c>SetIds</c>, <c>SetAttributes</c>).
    /// </summary>
    private Widget Instantiate(Type rootType)
    {
        var widget = (Widget) Activator.CreateInstance(rootType, CreateUIContext())!;
        foreach (var name in new[] { "CreateWidgets", "SetIds", "SetAttributes" })
            rootType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(widget, []);
        return widget;
    }

    private UIContext CreateUIContext()
    {
        var platform = Substitute.For<ITwoDimensionPlatform>();
        platform.ReferenceHeight.Returns(1080f);
        platform.ReferenceWidth.Returns(1920f);
        var twoDimension = new TwoDimensionContext(platform, Substitute.For<ITwoDimensionResourceContext>(), _workspace!.ResourceDepot);
        TestInput.EnsureInitialized();
        var context = new UIContext(twoDimension, Substitute.For<TaleWorlds.InputSystem.IInputContext>(), _spriteData!, new FontFactory(_workspace.ResourceDepot), _brushFactory!);
        // Initializes the EventManager required to handle layout invalidation when widget properties update.
        context.Initialize();
        return context;
    }
}
