using Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

using HarmonyLib;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace Bannerlord.UIExtenderEx.Tests;

/// <summary>
/// Verifies JIT inlining safety across patched TaleWorlds methods.
/// <para>
/// When RyuJIT inlines small target methods into callers compiled prior to patch application, those callers retain the
/// original inlined method body and bypass Harmony detours. Harmony marks patched methods with <see cref="System.Runtime.CompilerServices.MethodImplOptions.NoInlining"/>,
/// ensuring subsequent compilations invoke the detour. To prevent earlier callers from bypassing patches, every modified game method
/// must exceed inline thresholds, contain exception handling blocks, or have all upstream callers patched directly.
/// This test inspects TaleWorlds IL metadata from disk to detect any inlinable call paths lacking patch coverage.
/// </para>
/// <para>
/// Excludes dynamic <c>ViewModelWithMixinPatch</c> and <c>ViewModelOverridePatch</c> targets, whose callers and types are determined
/// by external mod registrations rather than fixed engine binaries.
/// </para>
/// </summary>
public class PatchInliningTests
{
    // RyuJIT on .NET Framework 4.8 and .NET 6 avoids inlining methods containing exception handlers or exceeding this IL byte budget.
    private const int MaxInlineIlSize = 100;

    private static readonly string[] Owners =
    [
        "bannerlord.uiextender.ex",
        "bannerlord.uiextender.ex.xmlprefabs",
        "bannerlord.uiextender.ex.compiledprefabs",
        "bannerlord.uiextender.ex.gameprefabs",
    ];

    // Tracks patched game methods exempted from caller coverage analysis.
    private static readonly Dictionary<string, string> Exempt = new();

    // Enumerates all game methods patched by UIExtenderEx runtimes to ensure every expected patch is active.
    private static readonly string[] Expected =
    [
        "TaleWorlds.GauntletUI.Data.GauntletMovie:Load",
        "TaleWorlds.GauntletUI.Data.GauntletMovie:Release",
        "TaleWorlds.GauntletUI.PrefabSystem.ConstantDefinition:GetValue",
        "TaleWorlds.Library.ViewModel:ExecuteCommand",
        "TaleWorlds.GauntletUI.PrefabSystem.WidgetPrefab:LoadFrom",
        "TaleWorlds.GauntletUI.BrushFactory:.ctor",
        "TaleWorlds.GauntletUI.BrushFactory:LoadBrushes",
        "TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:GetCustomType",
        "TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:CreateBuiltinWidget",
        "TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:GetWidgetTypes",
        "TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:IsCustomType",
        "TaleWorlds.GauntletUI.PrefabSystem.WidgetFactory:OnUnload",
        "TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate:CreateWidgets",
        "TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate:OnRelease",
        "TaleWorlds.GauntletUI.PrefabSystem.WidgetExtensions:GetObjectAndProperty",
        "TaleWorlds.GauntletUI.PrefabSystem.GeneratedPrefabContext:CollectPrefabs",
        // The leak is on a disconnect from v1.3.12, on a state taken away up to v1.3.11 (EventManagerPatch)
        AccessTools.DeclaredField(typeof(TaleWorlds.GauntletUI.EventManager), "_widgetContainers") is not null
            ? "TaleWorlds.GauntletUI.EventManager:OnWidgetDisconnectedFromRoot"
            : "TaleWorlds.GauntletUI.EventManager:UnRegisterWidgetForEvent",
    ];

    [Test]
    public void EveryPatchIsApplied()
    {
        var ours = new HashSet<string>(Harmony.GetAllPatchedMethods()
            .Where(x => Harmony.GetPatchInfo(x)?.Owners.Any(Owners.Contains) == true)
            .Select(x => $"{x.DeclaringType!.FullName}:{x.Name}"));

        Assert.That(Expected.Where(x => !ours.Contains(x)), Is.Empty, "Patches that were not applied");
    }

    [Test]
    public void EveryInlinablePatchedMethodHasItsCallersCovered()
    {
        if (TestGame.Directory is not { } gameDirectory)
        {
            Assert.Ignore("No game installation; set BANNERLORD_GAME_DIR to run this.");
            return;
        }

        var ours = Harmony.GetAllPatchedMethods()
            .Select(x => (Method: x, Info: Harmony.GetPatchInfo(x)))
            .Where(x => x.Info is not null && x.Info.Owners.Any(Owners.Contains))
            .ToList();
        var patched = new HashSet<string>(ours.Select(x => Key(x.Method)));
        var targets = ours
            .Where(x => x.Method.DeclaringType?.Assembly.GetName().Name.StartsWith("TaleWorlds.", StringComparison.Ordinal) == true)
            .Where(x => ChangesBehaviour(x.Info))
            .Select(x => x.Method)
            .ToList();
        Assert.That(targets, Is.Not.Empty, "UIExtenderEx's patches were not found; HarmonyBootstrap should have applied them");

        var game = GameIndex.Read(gameDirectory);
        var report = new List<string>();
        var missing = new List<string>();
        foreach (var target in targets)
        {
            var key = Key(target);
            Assert.That(game.Methods.ContainsKey(key), Is.True, $"{key} is patched but was not found in the game's assemblies, so the scan cannot be trusted");

            if (!game.Methods[key].IsInlinable)
            {
                report.Add($"{key}: cannot be inlined ({game.Methods[key]})");
                continue;
            }
            if (Exempt.TryGetValue(key, out var reason))
            {
                report.Add($"{key}: inlinable ({game.Methods[key]}), exempt: {reason}");
                continue;
            }

            var covered = new List<string>();
            Walk(key, [key]);
            report.Add($"{key}: inlinable ({game.Methods[key]}), covered by {(covered.Count == 0 ? "nothing, no game method calls it" : string.Join(", ", covered))}");

            void Walk(string callee, List<string> path)
            {
                if (!game.Callers.TryGetValue(callee, out var callers))
                    return;

                foreach (var caller in callers.OrderBy(x => x, StringComparer.Ordinal))
                {
                    if (path.Contains(caller))
                        continue;

                    var chain = new List<string>(path) { caller };
                    // A caller that can be inlined itself carries the body further up, patched or not
                    if (game.Methods.TryGetValue(caller, out var info) && info.IsInlinable)
                        Walk(caller, chain);
                    else if (patched.Contains(caller))
                        covered.Add(caller);
                    else
                        missing.Add(string.Join(" <- ", chain));
                }
            }
        }

        TestContext.Out.WriteLine(string.Join(Environment.NewLine, report));
        Assert.That(missing, Is.Empty, "Game methods that can carry an inlined patched method without being patched themselves:" + Environment.NewLine +
                                       string.Join(Environment.NewLine, missing));
    }

    // Ignore transpilers that only force caller recompilation without altering control flow, as well as dynamic mixin hooks.
    private static bool ChangesBehaviour(HarmonyLib.Patches info) => info.Prefixes
        .Concat(info.Postfixes)
        .Concat(info.Finalizers)
        .Concat(info.Transpilers)
        .Where(x => Owners.Contains(x.owner))
        .Any(x => x.PatchMethod.Name != "BlankTranspiler"
                  && x.PatchMethod.DeclaringType?.Name != "ViewModelWithMixinPatch"
                  && x.PatchMethod.DeclaringType?.Name != "ViewModelOverridePatch");

    /// <summary>
    /// Verifies that overriding a small inlinable method takes effect for all callers compiled after mixin registration.
    /// </summary>
    [Test]
    public void AnOverrideOfAMethodSmallEnoughToInline_ReachesACallerCompiledAfterRegistration()
    {
        var extender = UIExtender.Create("TestModule.PatchInlining.Override");
        try
        {
            extender.Register([typeof(TinyOverrideMixin)]);
            extender.Enable();
            TinyOverrideVM.Log.Clear();

            CloseAScreen();

            Assert.That(TinyOverrideVM.Log, Is.EqualTo(new[] { "override", "body" }));
        }
        finally
        {
            extender.Deregister();
        }
    }

    /// <summary>
    /// Calls the overridden method in a dedicated non-inlined method to simulate late compilation after mixin registration.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void CloseAScreen() => new TinyOverrideVM().CloseScreen();

    private static string Key(MethodBase method) => $"{method.DeclaringType!.FullName}:{method.Name}/{method.GetParameters().Length}";

    private sealed class MethodFacts(int ilSize, int exceptionRegions, bool noInlining, bool hasBody)
    {
        private int IlSize { get; } = ilSize;
        private int ExceptionRegions { get; } = exceptionRegions;
        private bool NoInlining { get; } = noInlining;
        private bool HasBody { get; } = hasBody;

        public bool IsInlinable => HasBody && !NoInlining && ExceptionRegions == 0 && IlSize <= MaxInlineIlSize;
        public override string ToString() => HasBody ? $"{IlSize} bytes of IL, {ExceptionRegions} exception regions" : "no body";
    }

    /// <summary>Parses TaleWorlds assemblies and extracts method call graphs directly from PE metadata without assembly loading.</summary>
    private sealed class GameIndex
    {
        private static readonly Dictionary<ushort, OpCode> OpCodesByValue = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(x => (OpCode) x.GetValue(null)!)
            .ToDictionary(x => (ushort) x.Value);

        public Dictionary<string, MethodFacts> Methods { get; } = new();
        public Dictionary<string, HashSet<string>> Callers { get; } = new();

        public static GameIndex Read(string gameDirectory)
        {
            var index = new GameIndex();
            foreach (var path in GameAssemblies(gameDirectory))
                index.Add(path);
            return index;
        }

        // Enumerates engine binaries and official module assemblies, excluding offline code generator tools.
        private static IEnumerable<string> GameAssemblies(string gameDirectory)
        {
            var bin = Path.Combine(gameDirectory, "bin", "Win64_Shipping_Client");
            foreach (var path in Directory.GetFiles(bin, "TaleWorlds.*.dll"))
            {
                if (!Path.GetFileName(path).StartsWith("TaleWorlds.GauntletUI.CodeGenerator", StringComparison.Ordinal))
                    yield return path;
            }

            var official = new Regex(@"<ModuleType\s+value\s*=\s*""Official""", RegexOptions.IgnoreCase);
            foreach (var module in Directory.GetDirectories(Path.Combine(gameDirectory, "Modules")))
            {
                var subModule = Path.Combine(module, "SubModule.xml");
                var moduleBin = Path.Combine(module, "bin", "Win64_Shipping_Client");
                if (!File.Exists(subModule) || !Directory.Exists(moduleBin) || !official.IsMatch(File.ReadAllText(subModule)))
                    continue;

                foreach (var path in Directory.GetFiles(moduleBin, "*.dll"))
                    yield return path;
            }
        }

        private void Add(string path)
        {
            using var pe = new PEReader(File.OpenRead(path));
            if (!pe.HasMetadata)
                return;

            var md = pe.GetMetadataReader();
            foreach (var typeHandle in md.TypeDefinitions)
            {
                var type = md.GetTypeDefinition(typeHandle);
                var typeName = FullName(md, type);
                foreach (var methodHandle in type.GetMethods())
                {
                    var method = md.GetMethodDefinition(methodHandle);
                    var key = $"{typeName}:{md.GetString(method.Name)}/{ParameterCount(md, method.Signature)}";
                    var noInlining = (method.ImplAttributes & MethodImplAttributes.NoInlining) != 0;
                    if (method.RelativeVirtualAddress == 0)
                    {
                        Methods[key] = new(0, 0, noInlining, false);
                        continue;
                    }

                    var body = pe.GetMethodBody(method.RelativeVirtualAddress);
                    Methods[key] = new(body.Size, body.ExceptionRegions.Length, noInlining, true);
                    AddCalls(md, body, key);
                }
            }
        }

        // Inspects call, callvirt, and newobj instructions capable of being inlined by the JIT compiler.
        private void AddCalls(MetadataReader md, MethodBodyBlock body, string caller)
        {
            var il = body.GetILReader();
            while (il.RemainingBytes > 0)
            {
                ushort value = il.ReadByte();
                if (value == 0xFE)
                    value = (ushort) (0xFE00 | il.ReadByte());
                var opCode = OpCodesByValue[value];

                if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt || opCode == OpCodes.Newobj)
                {
                    if (MethodKey(md, MetadataTokens.EntityHandle(il.ReadInt32())) is { } callee)
                    {
                        if (!Callers.TryGetValue(callee, out var callers))
                            Callers[callee] = callers = new HashSet<string>();
                        callers.Add(caller);
                    }
                    continue;
                }

                // Computed before it is added: reading a switch's case count moves the reader past the count
                var operandSize = opCode.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 * il.ReadInt32(),
                    _ => 4,
                };
                il.Offset += operandSize;
            }
        }

        private static string? MethodKey(MetadataReader md, EntityHandle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                {
                    var method = md.GetMethodDefinition((MethodDefinitionHandle) handle);
                    return $"{FullName(md, md.GetTypeDefinition(method.GetDeclaringType()))}:{md.GetString(method.Name)}/{ParameterCount(md, method.Signature)}";
                }
                case HandleKind.MemberReference:
                {
                    var reference = md.GetMemberReference((MemberReferenceHandle) handle);
                    if (reference.GetKind() != MemberReferenceKind.Method)
                        return null;
                    // A member of a generic instantiation is never one of the patched methods
                    var owner = reference.Parent.Kind switch
                    {
                        HandleKind.TypeReference => FullName(md, (TypeReferenceHandle) reference.Parent),
                        HandleKind.TypeDefinition => FullName(md, md.GetTypeDefinition((TypeDefinitionHandle) reference.Parent)),
                        _ => null,
                    };
                    return owner is null ? null : $"{owner}:{md.GetString(reference.Name)}/{ParameterCount(md, reference.Signature)}";
                }
                case HandleKind.MethodSpecification:
                    return MethodKey(md, md.GetMethodSpecification((MethodSpecificationHandle) handle).Method);
                default:
                    return null;
            }
        }

        private static int ParameterCount(MetadataReader md, BlobHandle signature)
        {
            var reader = md.GetBlobReader(signature);
            if (reader.ReadSignatureHeader().IsGeneric)
                reader.ReadCompressedInteger();
            return reader.ReadCompressedInteger();
        }

        private static string FullName(MetadataReader md, TypeDefinition type)
        {
            var declaring = type.GetDeclaringType();
            return declaring.IsNil
                ? Join(md.GetString(type.Namespace), md.GetString(type.Name))
                : $"{FullName(md, md.GetTypeDefinition(declaring))}+{md.GetString(type.Name)}";
        }

        private static string FullName(MetadataReader md, TypeReferenceHandle handle)
        {
            var reference = md.GetTypeReference(handle);
            return reference.ResolutionScope.Kind == HandleKind.TypeReference
                ? $"{FullName(md, (TypeReferenceHandle) reference.ResolutionScope)}+{md.GetString(reference.Name)}"
                : Join(md.GetString(reference.Namespace), md.GetString(reference.Name));
        }

        private static string Join(string ns, string name) => ns.Length == 0 ? name : $"{ns}.{name}";
    }
}

public class TinyOverrideVM : TaleWorlds.Library.ViewModel
{
    public static readonly List<string> Log = [];

    // Emits a minimal IL body small enough to qualify for JIT inlining prior to patching.
    public void Close() => Log.Add("body");

    // Compiles on first execution following mixin registration.
    public void CloseScreen() => Close();
}

[Bannerlord.UIExtenderEx.Attributes.ViewModelMixin]
public class TinyOverrideMixin : Bannerlord.UIExtenderEx.ViewModels.BaseViewModelMixin<TinyOverrideVM>
{
    public TinyOverrideMixin(TinyOverrideVM vm) : base(vm) { }

    [Bannerlord.UIExtenderEx.Attributes.BUTRViewModelOverride(nameof(TinyOverrideVM.Close))]
    private void Close(Action original)
    {
        TinyOverrideVM.Log.Add("override");
        original();
    }
}