using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.CompiledPrefabs.Compilation;
using Bannerlord.UIExtenderEx.GamePrefabs;
using Bannerlord.UIExtenderEx.Patches;
using Bannerlord.UIExtenderEx.ResourceManager;

using HarmonyLib;
using HarmonyLib.BUTR.Extensions;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Executes code generation over a prefab binding to members contributed by a ViewModel mixin,
/// compiles the generated source, and loads the resulting assembly into GauntletUI.
/// </summary>
public class CompiledPrefabGenerationTests
{
    private const string MovieName = "CodegenMixinMovie";

    private const string Prefab = @"
<Prefab>
  <Window>
    <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"">
      <Children>
        <TextWidget Id=""TitleText"" Text=""@Title"" IsEnabled=""@IsEnabled"" />
        <TextWidget Id=""MixinText"" Text=""@MixinText"" />
        <TextWidget Id=""EditableText"" Text=""@EditableText"" />
        <ButtonWidget Id=""CloseButton"" Command.Click=""ExecuteClose"" />
        <ButtonWidget Id=""MixinButton"" Command.Click=""ExecuteMixinCommand"" />
        <Widget Id=""ChildPanel"" DataSource=""{Child}"">
          <Children>
            <TextWidget Id=""ChildText"" Text=""@ChildText"" />
          </Children>
        </Widget>
        <ListPanel Id=""Items"" DataSource=""{Items}"">
          <ItemTemplate>
            <TextWidget Text=""@ItemText"" />
          </ItemTemplate>
        </ListPanel>
      </Children>
    </Widget>
  </Window>
</Prefab>
";

    /// <summary>Specifies a movie containing only leaf bindings that generate even when the mixin is disabled and its members become unknown.</summary>
    private const string SimpleMovieName = "CodegenSimpleMovie";

    private const string SimplePrefab = @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <TextWidget Id=""TitleText"" Text=""@Title"" />
        <TextWidget Id=""MixinText"" Text=""@MixinText"" />
        <ButtonWidget Id=""MixinButton"" Command.Click=""ExecuteMixinCommand"" />
      </Children>
    </Widget>
  </Window>
</Prefab>
";

    /// <summary>Specifies a movie that binds only to internal types: an internal mixin, its command, and a nested internal ViewModel.</summary>
    private const string InternalMovieName = "CodegenInternalMovie";

    private const string InternalPrefab = @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <TextWidget Id=""InternalMixinText"" Text=""@InternalMixinText"" />
        <ButtonWidget Id=""InternalButton"" Command.Click=""ExecuteInternalCommand"" />
        <Widget Id=""InternalChildPanel"" DataSource=""{InternalChild}"">
          <Children>
            <TextWidget Id=""InternalText"" Text=""@InternalText"" />
          </Children>
        </Widget>
      </Children>
    </Widget>
  </Window>
</Prefab>
";

    /// <summary>Specifies a movie using a widget class and a prefab registered dynamically at runtime.</summary>
    private const string RegisteredMovieName = "CodegenRegisteredMovie";

    private const string RegisteredPrefab = @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <CodegenRegisteredWidget Id=""RegisteredWidget"" />
        <CodegenRegisteredPrefab Id=""RegisteredPrefab"" />
      </Children>
    </Widget>
  </Window>
</Prefab>
";

    private const string RegisteredNestedPrefabName = "CodegenRegisteredPrefab";

    private const string RegisteredNestedPrefab = @"
<Prefab>
  <Window>
    <TextWidget Text=""@Title"" />
  </Window>
</Prefab>
";

    private const string UnknownWidgetMovieName = "CodegenUnknownWidgetMovie";

    private const string UnknownWidgetPrefab = @"
<Prefab>
  <Window>
    <Widget>
      <Children>
        <NoSuchWidget />
      </Children>
    </Widget>
  </Window>
</Prefab>
";

    private static readonly AccessTools.FieldRef<WidgetFactory, Dictionary<string, Type>>? BuiltinTypes =
        AccessTools2.FieldRefAccess<WidgetFactory, Dictionary<string, Type>>("_builtinTypes");

    private static readonly AccessTools.FieldRef<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>? GeneratedPrefabs =
        AccessTools2.FieldRefAccess<GeneratedPrefabContext, Dictionary<string, Dictionary<string, CreateGeneratedWidget>>>("_generatedPrefabs");

    private static readonly string Mixin = "global::" + typeof(CodegenTestVMMixin).FullName!;
    private static readonly string MixinAccess = $"global::Bannerlord.UIExtenderEx.ViewModels.ViewModelMixins.Get<{Mixin}>(_datasource_Root)";

    private PrefabWorkspace? _workspace;
    private UIExtender? _extender;


    [SetUp]
    public void SetUp()
    {
        _extender = UIExtender.Create("TestModule.CompiledPrefabs.Generation");
        _extender.Register([typeof(CodegenTestVMMixin), typeof(CodegenInternalMixin)]);
        _extender.Enable();
        _workspace = new PrefabWorkspace((MovieName, Prefab), (SimpleMovieName, SimplePrefab), (InternalMovieName, InternalPrefab), (RegisteredMovieName, RegisteredPrefab), (UnknownWidgetMovieName, UnknownWidgetPrefab));

        WidgetFactoryManager.Register(typeof(CodegenRegisteredWidget));
        if (!WidgetFactoryManager.IsRegisteredCustomType(RegisteredNestedPrefabName))
        {
            WidgetFactoryManager.Register(RegisteredNestedPrefabName, () =>
            {
                var document = new XmlDocument();
                document.LoadXml(RegisteredNestedPrefab);
                return WidgetPrefabPatch.LoadFromDocument(_workspace!.WidgetFactory.PrefabExtensionContext, _workspace.WidgetFactory.WidgetAttributeContext, RegisteredNestedPrefabName + ".xml", document);
            });
        }
    }

    [TearDown]
    public void TearDown()
    {
        _extender?.Deregister();
        _workspace?.Dispose();
    }

    [Test]
    public void RegisteredWidgetClassesAndPrefabs_AreGeneratedLikeTheGamesOwn()
    {
        // Remove the test class from the factory table to simulate runtime conditions where the mod assembly is loaded after scanning.
        // Ensures the generator discovers dynamically registered types via UIExtenderEx patches in parity with the XML loader.
        BuiltinTypes!(_workspace!.WidgetFactory).Remove(nameof(CodegenRegisteredWidget));
        Assert.That(_workspace.WidgetFactory.IsBuiltinType(nameof(CodegenRegisteredWidget)), Is.False);
        Assert.That(_workspace.WidgetFactory.GetCustomTypePath(RegisteredNestedPrefabName), Is.Empty, "the prefab exists only through its registration, not on disk");

        var code = string.Join("\n", _workspace.Generate(RegisteredMovieName, typeof(CodegenTestVM)).Select(x => x.Content));

        Assert.That(code, Does.Contain("new global::" + typeof(CodegenRegisteredWidget).FullName + "(this.Context)"));
        Assert.That(code, Does.Contain(RegisteredNestedPrefabName + "__DependendPrefab"), "the registered prefab is inlined as a dependency like any prefab from disk");
        Assert.That(code, Does.Contain("_datasource_Root.Title"), "and its bindings are generated");
    }

    /// <summary>
    /// Verifies that an undefined widget type falls back to a base <see cref="Widget"/> instance accompanied by the
    /// <c>WidgetFactory.CreateBuiltinWidget</c> assertion, matching XML loader fallback semantics.
    /// </summary>
    [Test]
    public void UnknownWidgetType_IsAPlainWidgetWithTheLoadersAssert()
    {
        var code = string.Join("\n", _workspace!.Generate(UnknownWidgetMovieName, typeof(CodegenTestVM)).Select(x => x.Content));

        Assert.That(code, Does.Contain("new global::TaleWorlds.GauntletUI.BaseTypes.Widget(this.Context)"));
        Assert.That(code, Does.Contain("Debug.FailedAssert(\"builtin widget type not found in CreateBuiltinWidget(NoSuchWidget)\")"));
    }

    [Test]
    public void Generation_LeavesNoPrefabPinnedInTheFactory()
    {
        GenerateMovie();

        Assert.That(_workspace!.LivePrefabNames, Is.Empty, "the generator reads prefabs through the factory's live counting and must release them");
    }

    [Test]
    public void ViewModelMembers_KeepTypedAccess()
    {
        var code = GenerateMovie();

        Assert.That(code, Does.Contain("_datasource_Root.Title"));
        Assert.That(code, Does.Contain("_datasource_Root.IsEnabled"));
        Assert.That(code, Does.Contain("_datasource_Root.ExecuteClose();"));
    }

    [Test]
    public void MixinProperty_IsReadThroughMixinInstance()
    {
        var code = GenerateMovie();

        // Verify that the generator caches the mixin instance per data source rather than resolving it on every read.
        Assert.That(code, Does.Contain(" = " + MixinAccess + ";"));
        Assert.That(code, Does.Match(@"var mixin = GetMixin_\d+\(\);"));
        Assert.That(code, Does.Contain(" = mixin.MixinText;"));
        Assert.That(code, Does.Not.Contain("Couldn't find property in ViewModel"));
    }

    [Test]
    public void MixinProperty_WithSetter_IsWrittenBackThroughMixinInstance()
    {
        var code = GenerateMovie();

        Assert.That(code, Does.Contain("mixin.EditableText = "));
    }

    [Test]
    public void MixinCommand_IsInvokedThroughMixinInstance()
    {
        var code = GenerateMovie();

        Assert.That(code, Does.Contain("mixin.ExecuteMixinCommand();"));
        Assert.That(code, Does.Not.Contain("Couldn't find method"));
    }

    [Test]
    public void MixinViewModel_IsNavigatedThroughItsMixinReceiver()
    {
        var code = GenerateMovie();
        var child = "global::" + typeof(CodegenChildVM).FullName;

        Assert.That(code, Does.Match(@"_datasource_Root_Child = GetMixin_\d+\(\)\?\.Child;"));
        Assert.That(code, Does.Contain($"private void RefreshDataSource_datasource_Root_Child({child} newDataSource)"));
        Assert.That(code, Does.Contain("_datasource_Root_Child.ChildText"));
    }

    [Test]
    public void MixinBindingList_IsNavigatedThroughItsMixinReceiver()
    {
        var code = GenerateMovie();

        Assert.That(code, Does.Match(@"GetMixin_\d+\(\)\?\.Items"));
        Assert.That(code, Does.Contain("_datasource_Root_Items.ListChanged += "));
    }

    [Test]
    public void DisabledMixin_IsNotBound()
    {
        _extender!.Disable();

        var code = _workspace!.Generate(SimpleMovieName, typeof(CodegenTestVM)).Single(x => x.FileName == SimpleMovieName + ".gen.cs").Content;

        Assert.That(code, Does.Contain("_datasource_Root.Title"));
        // Emit late-bound dynamic member lookups when the mixin is disabled, matching XML loader fallback semantics.
        Assert.That(code, Does.Contain("DynamicMember.Get(_datasource_Root, \"MixinText\")"));
        Assert.That(code, Does.Contain("DynamicMember.Execute(_datasource_Root, \"ExecuteMixinCommand\", arguments)"));
        Assert.That(code, Does.Not.Contain(MixinAccess));
    }

    /// <summary>
    /// Verifies that data source paths lacking compile-time types emit dynamic member lookups against the instance,
    /// matching XML loader binding semantics when mixins are disabled.
    /// </summary>
    [Test]
    public void DataSourcePathsThatLoseTheirType_AreGeneratedByName()
    {
        _extender!.Disable();

        var code = GenerateMovie();

        Assert.That(code, Does.Contain("private global::TaleWorlds.Library.ViewModel _datasource_Root_Child;"));
        // Store untyped object references and dynamically cast to ViewModel when bound.
        Assert.That(code, Does.Contain("_datasource_Root_Child_object = global::Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime.DynamicMember.GetChild(_datasource_Root, \"Child\");"));
        Assert.That(code, Does.Contain("_datasource_Root_Child = _datasource_Root_Child_object as global::TaleWorlds.Library.ViewModel;"));
        Assert.That(code, Does.Contain("private global::TaleWorlds.Library.IMBBindingList _datasource_Root_Items;"));
        Assert.That(code, Does.Contain("DynamicMember.GetChild(_datasource_Root, \"Items\") as global::TaleWorlds.Library.IMBBindingList"));
    }

    /// <summary>
    /// Verifies that an item template defined over a non-list <see cref="ViewModel"/> binds the widget directly against
    /// the <see cref="ViewModel"/> without instantiating item template classes, matching <c>GauntletView.RefreshBinding</c> semantics.
    /// </summary>
    [Test]
    public void AnItemTemplateOverSomethingThatIsNotAList_IsBoundAsAPlainWidget()
    {
        using var workspace = new PrefabWorkspace(("NotAListMovie", @"
<Prefab><Window><Widget><Children>
  <ListPanel DataSource=""{Child}"">
    <ItemTemplate><TextWidget Text=""@ChildText"" /></ItemTemplate>
  </ListPanel>
</Children></Widget></Window></Prefab>"));

        var code = string.Join(Environment.NewLine, workspace.Generate("NotAListMovie", typeof(CodegenChildHolderVM)).Select(x => x.Content));

        Assert.That(code, Does.Not.Contain("_ItemTemplate"), "no item is ever built from it");
        Assert.That(code, Does.Contain("_datasource_Root_Child.PropertyChanged +="), "the widget is bound against the ViewModel");
    }

    [Test]
    public void CreatorClass_RegistersTheVariant()
    {
        var creatorCode = _workspace!.Generate(MovieName, typeof(CodegenTestVM)).Single(x => x.FileName == "PrefabCodes.gen.cs").Content;

        Assert.That(creatorCode, Does.Contain("class GeneratedUIPrefabCreator"));
        // Omit IGeneratedUIPrefabCreator implementation to prevent TaleWorlds prefab scanning from registering unvalidated builds.
        Assert.That(creatorCode, Does.Not.Contain("IGeneratedUIPrefabCreator"));
        Assert.That(creatorCode, Does.Contain("public void CollectGeneratedPrefabDefinitions(global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext generatedPrefabContext)"));
        Assert.That(creatorCode, Does.Contain($"generatedPrefabContext.AddGeneratedPrefab(\"{MovieName}\", \"{typeof(CodegenTestVM).FullName}\", Create"));
    }

    [Test]
    public void Compile_ProducesLoadableCreator()
    {
        var compiler = new RoslynCompiler();

        var result = Compile(compiler, MovieName);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));

        AssertCreatorRegisters(result.Assembly!, MovieName);
    }

    [Test]
    public void InternalMixinAndViewModel_CompileWithRoslyn()
    {
        // Configure Roslyn to bypass accessibility checks and declare IgnoresAccessChecksTo attributes for referenced assemblies.
        var compiler = new RoslynCompiler();

        var code = _workspace!.Generate(InternalMovieName, typeof(CodegenTestVM)).Single(x => x.FileName == InternalMovieName + ".gen.cs").Content;
        Assert.That(code, Does.Contain(typeof(CodegenInternalMixin).FullName));
        Assert.That(code, Does.Contain($"RefreshDataSource_datasource_Root_InternalChild(global::{typeof(CodegenInternalChildVM).FullName} newDataSource)"));

        var result = Compile(compiler, InternalMovieName);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));

        AssertCreatorRegisters(result.Assembly!, InternalMovieName);
    }

    [Test]
    public void Publicizer_MakesEveryTypePublicInTheCompileTimeCopy()
    {
        // Target Bannerlord.UIExtenderEx to verify publicizing on internal types typical of third-party mod assemblies.
        var sample = typeof(GauntletMoviePatch);
        var path = sample.Assembly.Location;
        Assert.That(sample.IsPublic, Is.False, "test premise: an internal type");

        var image = ReferencePublicizer.Publicize(path);

        Assert.That(image, Is.Not.EqualTo(System.IO.File.ReadAllBytes(path)), "something was patched");

        // Inspect raw byte image without loading into AppDomain to preserve module IDs and prevent Harmony token collisions.
        static List<(string Name, System.Reflection.TypeAttributes Visibility, bool Nested)> Types(byte[] bytes)
        {
            using var pe = new System.Reflection.PortableExecutable.PEReader(new System.IO.MemoryStream(bytes));
            var md = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            // Skip the <Module> entry at row index 0.
            return md.TypeDefinitions.Skip(1)
                .Select(md.GetTypeDefinition)
                .Select(x => ($"{md.GetString(x.Namespace)}.{md.GetString(x.Name)}", x.Attributes & System.Reflection.TypeAttributes.VisibilityMask, !x.GetDeclaringType().IsNil))
                .ToList();
        }
        var publicized = Types(image);

        Assert.That(publicized.Single(x => x.Name == sample.FullName).Visibility, Is.EqualTo(System.Reflection.TypeAttributes.Public));
        Assert.That(publicized.Where(x => !x.Nested).All(x => x.Visibility == System.Reflection.TypeAttributes.Public), Is.True, "every top-level type");
        Assert.That(publicized.Where(x => x.Nested).All(x => x.Visibility == System.Reflection.TypeAttributes.NestedPublic), Is.True, "every nested type");
        Assert.That(publicized, Has.Count.EqualTo(Types(System.IO.File.ReadAllBytes(path)).Count), "no type lost");
    }

    [Test]
    public void Publicizer_LeavesNonManagedFilesAlone()
    {
        var path = System.IO.Path.Combine(_workspace!.Root, "not-an-assembly.dll");
        System.IO.File.WriteAllBytes(path, [1, 2, 3, 4]);

        Assert.That(ReferencePublicizer.Publicize(path), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
    }

    [Test]
    public void IgnoresAccessChecks_CoversEveryNonFrameworkReference()
    {
        var source = IgnoresAccessChecksSource.Create([@"C:\game\mscorlib.dll", @"C:\game\System.Numerics.Vectors.dll", @"C:\game\netstandard.dll", @"C:\game\TaleWorlds.Library.dll", @"C:\mods\SomeMod.dll", @"C:\mods\SomeMod.dll"]);

        Assert.That(source.FileName, Is.EqualTo("IgnoresAccessChecks.gen.cs"));
        Assert.That(source.Content, Does.Contain("internal sealed class IgnoresAccessChecksToAttribute : Attribute"));
        Assert.That(source.Content, Does.Contain("[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"SomeMod\")]"));
        Assert.That(source.Content, Does.Contain("[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"TaleWorlds.Library\")]"));
        Assert.That(source.Content, Does.Not.Contain("\"mscorlib\""));
        Assert.That(source.Content, Does.Not.Contain("\"netstandard\""));
        Assert.That(source.Content, Does.Not.Contain("\"System.Numerics.Vectors\""));
        Assert.That(source.Content.Split(["SomeMod"], StringSplitOptions.None).Length, Is.EqualTo(2), "listed once");
    }

    private CompilationResult Compile(ICSharpCompiler compiler, string movieName)
    {
        var references = PrefabReferenceSet.CollectPaths(_workspace!.WidgetFactory, typeof(CodegenTestVM));
        var sources = _workspace.Generate(movieName, typeof(CodegenTestVM)).Concat([IgnoresAccessChecksSource.Create(references)]).ToList();
        // Assign the standard generated assembly naming pattern so the runtime identifies it as an extender-managed build.
        return compiler.Compile(CompiledPrefabManager.GetAssemblyName(movieName, typeof(CodegenTestVM), "testbuild0000000"), sources, references);
    }

    private void AssertCreatorRegisters(byte[] assembly, string movieName)
    {
        var environment = new GameCompiledPrefabEnvironment();
        var creator = environment.CreateCreator(environment.LoadAssembly(assembly));
        Assert.That(creator, Is.Not.Null);

        // Verify that the generated assembly conforms to extender naming conventions so movie patches distinguish it from TaleWorlds prefabs.
        Assert.That(CompiledPrefabManager.IsGeneratedAssembly(creator!.Method.DeclaringType!.Assembly), Is.True);

        // Verify that all generated widget types exist in WidgetInfo static lookup tables to prevent base Widget constructor failures.
        var widgetTypes = creator!.Method.DeclaringType!.Assembly.GetTypes().Where(x => typeof(Widget).IsAssignableFrom(x)).ToList();
        Assert.That(widgetTypes, Is.Not.Empty);
        foreach (var widgetType in widgetTypes)
            Assert.That(() => WidgetInfo.GetWidgetInfo(widgetType), Throws.Nothing, widgetType.Name + " is unknown to WidgetInfo");

        var context = new GeneratedPrefabContext();
        creator(context);

        Assert.That(GeneratedPrefabs, Is.Not.Null);
        var registered = GeneratedPrefabs!(context);
        Assert.That(registered.ContainsKey(movieName), Is.True);
        Assert.That(registered[movieName].ContainsKey(typeof(CodegenTestVM).FullName!), Is.True);

        // Resolve the root widget type directly through the generated creator delegate without scanning loaded assemblies.
        var rootType = GamePrefabRuntime.GetRootWidgetType(registered[movieName][typeof(CodegenTestVM).FullName!]);
        Assert.That(rootType, Is.Not.Null);
        Assert.That(rootType!.Name, Does.StartWith(movieName + "__"));
        Assert.That(typeof(Widget).IsAssignableFrom(rootType), Is.True);
        Assert.That(GamePrefabRuntime.GetAutoGenNames(rootType), Does.Contain(movieName));

        // Ensure rescanned generated widget types in the factory do not become fingerprint inputs or compilation references.
        var factory = _workspace!.WidgetFactory;
        BuiltinTypes!(factory)[rootType.Name] = rootType;
        try
        {
            PrefabFingerprint.Compute(factory, movieName, typeof(CodegenTestVM), out var inputs);
            var generatedAssembly = rootType.Assembly.GetName().Name!;
            Assert.That(inputs, Does.Not.Contain(generatedAssembly), "a compiled prefab is never an input of a fingerprint");
            Assert.That(PrefabReferenceSet.CollectPaths(factory, typeof(CodegenTestVM)).Select(Path.GetFileNameWithoutExtension), Has.None.EqualTo(generatedAssembly));
        }
        finally
        {
            BuiltinTypes!(factory).Remove(rootType.Name);
        }
    }

    [Test]
    public void RoslynCompiler_TouchesNoRoslynTypeBeforeItCompiles()
    {
        // Ensure Roslyn types are isolated in nested types to prevent premature initialization during eager JIT passes by crash reporters.
        var fields = typeof(RoslynCompiler).GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(fields.Where(x => MentionsRoslyn(x.FieldType)).Select(x => x.Name), Is.Empty);
    }

    /// <summary>
    /// Determines whether the specified type belongs to Roslyn by inspecting its namespace prefix, accounting for ILRepacked assemblies.
    /// </summary>
    private static bool MentionsRoslyn(Type type) =>
        type.FullName?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) == true
        || (type.IsGenericType && type.GetGenericArguments().Any(MentionsRoslyn));

    [Test]
    public void WarmUp_CompilesTheWarmUpSnippetAgainstTheGameAssemblies()
    {
        var compiler = new RoslynCompiler();

        var result = compiler.Compile("Bannerlord.UIExtenderEx.Tests.WarmUp",
            [new GeneratedSource("WarmUp.gen.cs", (string) typeof(RoslynCompiler).GetField("WarmUpSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!)],
            PrefabReferenceSet.CollectPaths(null, null));

        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));
    }

    /// <summary>
    /// Verifies that Roslyn executes from the bundled, internalized compiler assembly without resolving external assembly copies at runtime.
    /// <para>
    /// Bundling and internalizing Roslyn avoids assembly binding collisions with game dependencies (such as ButterLib) that ship
    /// mismatched versions of <c>System.Collections.Immutable</c> and <c>System.Reflection.Metadata</c>.
    /// </para>
    /// </summary>
    [Test]
    public void TheRoslynUsedForCompiling_IsTheOneBundledInThisAssembly()
    {
        var compiler = new RoslynCompiler();

        var result = compiler.Compile("Bannerlord.UIExtenderEx.Tests.RoslynBinding",
            [new GeneratedSource("Empty.gen.cs", "namespace Bannerlord.UIExtenderEx.Tests { internal class Empty { } }")],
            PrefabReferenceSet.CollectPaths(null, null));

        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Errors));

        Assert.That(typeof(RoslynCompiler).Assembly.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation", false), Is.Not.Null,
            "the compiler was not merged into this assembly");
        Assert.That(AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetName().Name), Has.None.StartWith("Microsoft.CodeAnalysis"),
            "a compilation just ran, so if any Roslyn assembly were resolved by name it would be loaded now");
    }

    [Test]
    public void ReferencePaths_ContainOnlyAssembliesReachableFromTheGeneratedCode()
    {
        // Identify assemblies loaded in the current process but unreachable by generated code, such as test runners or BLSE modules.
        var unrelated = new List<string>();
        if (Assembly.GetEntryAssembly()?.GetName().Name is { } entryAssembly)
            unrelated.Add(entryAssembly);
        // The test adapter is always next to the tests, and nothing here references it. Needed where there is no entry
        // assembly (net472 runs the tests in their own AppDomain) and no BLSE (a game downloaded from the depot).
        var adapter = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NUnit3.TestAdapter.dll");
        if (System.IO.File.Exists(adapter))
            unrelated.Add(Assembly.LoadFrom(adapter).GetName().Name);
        var blse = new[] { Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"), @"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord" }
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => System.IO.Path.Combine(x!, "bin", "Win64_Shipping_Client", "Bannerlord.BLSE.Shared.dll"))
            .FirstOrDefault(System.IO.File.Exists);
        if (blse is not null)
            unrelated.Add(Assembly.LoadFrom(blse).GetName().Name);
        Assert.That(unrelated, Is.Not.Empty, "test setup: an unrelated assembly is needed");

        var paths = PrefabReferenceSet.CollectPaths(_workspace!.WidgetFactory, typeof(CodegenTestVM));
        var names = paths.Select(System.IO.Path.GetFileNameWithoutExtension).ToList();

        // Verify core runtime assemblies required by generated bindings.
        Assert.That(names, Does.Contain("TaleWorlds.Library"));
        Assert.That(names, Does.Contain("TaleWorlds.GauntletUI"));
        Assert.That(names, Does.Contain("TaleWorlds.GauntletUI.PrefabSystem"));
        Assert.That(names, Does.Contain("Bannerlord.UIExtenderEx"));
        Assert.That(names, Does.Contain(typeof(CodegenTestVM).Assembly.GetName().Name), "ViewModel and mixin assembly");

        foreach (var name in unrelated)
            Assert.That(names, Does.Not.Contain(name));
        Assert.That(names, Has.None.StartsWith("Microsoft.CodeAnalysis"));
        Assert.That(names, Is.Unique);

        // Ensure exactly one assembly in the reference set provides the canonical System.Numerics.Vector2 definition.
        var referenced = AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic && names.Contains(x.GetName().Name)).ToList();
        var definingVector2 = referenced.Where(x => x.GetType("System.Numerics.Vector2", false) is { } type && type.Assembly == x).Select(x => x.GetName().Name).ToList();
        Assert.That(definingVector2, Has.Count.EqualTo(1), string.Join(", ", definingVector2));
    }

    private string GenerateMovie() => _workspace!.Generate(MovieName, typeof(CodegenTestVM)).Single(x => x.FileName == MovieName + ".gen.cs").Content;
}
