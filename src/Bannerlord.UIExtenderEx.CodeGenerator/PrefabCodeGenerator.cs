using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.CSharp;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Prefabs;

using System;
using System.Collections.Generic;

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library.CodeGeneration;
using TaleWorlds.TwoDimension;

namespace Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator;

/// <summary>
/// Generates C# source code for compiled Gauntlet UI movies, emitting one source file per movie containing its widget classes and an entry-point registration file.
/// </summary>
public class PrefabCodeGenerator
{
    /// <summary>Defines the type name of the generated entry-point class instantiated during assembly registration.</summary>
    public const string CreatorClassName = "GeneratedUIPrefabCreator";

    /// <summary>Defines the method name executed to register generated movie creators into <see cref="GeneratedPrefabContext"/>.</summary>
    public const string CollectMethodName = "CollectGeneratedPrefabDefinitions";

    private readonly List<PrefabClass> _movies = [];

    private readonly string _nameSpace;

    public WidgetFactory WidgetFactory { get; }

    public BrushFactory BrushFactory { get; }

    public SpriteData SpriteData { get; }

    private readonly List<string> _warnings = [];

    /// <summary>
    /// Gets non-fatal warnings detected during code generation, such as undefined widget type references.
    /// </summary>
    /// <remarks>
    /// Retains unique diagnostic messages for reporting; generated code mirrors XML loader assertion behavior at instantiation time.
    /// </remarks>
    public IReadOnlyList<string> Warnings => _warnings;

    internal void Warn(string message)
    {
        if (!_warnings.Contains(message))
        {
            _warnings.Add(message);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrefabCodeGenerator"/> class using active UI resources and factories.
    /// </summary>
    /// <param name="nameSpace">The target C# namespace for generated classes.</param>
    /// <param name="widgetFactory">The widget factory resolving prefabs and widgets.</param>
    /// <param name="spriteData">The sprite resource repository.</param>
    /// <param name="brushFactory">The brush resource factory.</param>
    /// <remarks>
    /// Reuses existing UI resources. Assumes <see cref="WidgetFactory"/> instances already have <c>PrefabDatabindingExtension</c> attached.
    /// </remarks>
    public PrefabCodeGenerator(string nameSpace, WidgetFactory widgetFactory, SpriteData spriteData, BrushFactory brushFactory)
    {
        _nameSpace = nameSpace;
        WidgetFactory = widgetFactory;
        SpriteData = spriteData;
        BrushFactory = brushFactory;
    }

    /// <summary>
    /// Queues a movie for code generation, binding against the specified ViewModel data source type.
    /// </summary>
    /// <param name="prefabName">The root prefab name.</param>
    /// <param name="variantName">The variant identifier.</param>
    /// <param name="dataSourceType">The ViewModel type bound to the movie, or <see langword="null"/> to generate an unbound widget tree.</param>
    public void AddMovie(string prefabName, string variantName, Type? dataSourceType)
    {
        _movies.Add(PrefabClass.CreateMovie(this, prefabName, variantName, new PrefabClassOptions(dataSourceType)));
    }

    /// <summary>
    /// Generates C# source text for all added movies in memory.
    /// </summary>
    /// <returns>A list of file name and source text pairs.</returns>
    public List<KeyValuePair<string, string>> GenerateInMemory()
    {
        var generatedFiles = new List<KeyValuePair<string, string>>();

        // Emits one source file per prefab containing all compiled variants.
        var contextsByFileName = new Dictionary<string, CodeGenerationContext>();
        foreach (var movie in _movies)
        {
            var fileName = $"{movie.PrefabName}.gen.cs";
            if (!contextsByFileName.TryGetValue(fileName, out var context))
            {
                contextsByFileName.Add(fileName, context = new());
            }
            movie.GenerateInto(context.FindOrCreateNamespace(_nameSpace));
        }
        // Emits fully qualified global:: type references without using directives to ensure deterministic dependency binding.
        foreach (var (fileName, codeGenerationContext) in contextsByFileName)
        {
            var codeGenerationFile = new CodeGenerationFile();
            codeGenerationContext.GenerateInto(codeGenerationFile);
            generatedFiles.Add(new(fileName, codeGenerationFile.GenerateText()));
        }

        // Emits the entry point class that registers all compiled prefabs into the GeneratedPrefabContext.
        var creatorContext = new CodeGenerationContext();
        var creatorClassCode = new ClassCode
        {
            Name = CreatorClassName,
            AccessModifier = ClassCodeAccessModifier.Public,
        };
        // Omits IGeneratedUIPrefabCreator implementation to prevent TaleWorlds' automatic assembly scan from registering unverified builds; registration is performed explicitly via CompiledPrefabManager upon fingerprint verification.
        var collectMethod = new MethodCode
        {
            Name = CollectMethodName,
            MethodSignature = "(global::TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext generatedPrefabContext)",
        };
        foreach (var movie in _movies)
        {
            var creatorMethod = movie.GenerateCreatorMethod();
            creatorClassCode.AddMethod(creatorMethod);
            collectMethod.AddLine($"generatedPrefabContext.AddGeneratedPrefab({GeneratedLiteral.Regular(movie.PrefabName)}, {GeneratedLiteral.Regular(movie.VariantName)}, {creatorMethod.Name});");
        }
        creatorClassCode.AddMethod(collectMethod);
        creatorContext.FindOrCreateNamespace(_nameSpace).AddClass(creatorClassCode);
        var creatorFile = new CodeGenerationFile();
        creatorContext.GenerateInto(creatorFile);
        generatedFiles.Add(new("PrefabCodes.gen.cs", creatorFile.GenerateText()));
        return generatedFiles;
    }
}