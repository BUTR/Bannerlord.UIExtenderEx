using Bannerlord.UIExtenderEx.CompiledPrefabs;
using Bannerlord.UIExtenderEx.Extensions;
using Bannerlord.UIExtenderEx.GauntletUI.CodeGenerator.Runtime;
using Bannerlord.UIExtenderEx.Tests.CodeGenerator;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Tests the typed fast path runtime and generated code against live widgets, comparing direct property assignments
/// directly against fallback <c>SetWidgetAttribute</c> invocations.
/// <para>
/// Verifies behavioral equivalence across runtime execution paths: type guards, implicit widening conversions,
/// exception propagation, and invocation counts. Compares compiled movie outcomes against widgets manipulated
/// directly via the standard XML loader attribute reflection path.
/// </para>
/// </summary>
public class TypedPayloadFastPathTests
{
    private const string Movie = "FastPathDrivenMovie";

    /// <summary>
    /// Prefab XML template covering all data-source binding scenarios across multiple widgets, including eligible
    /// fast-path candidates, widened types, dotted paths, boxed properties, enums, and absent properties.
    /// </summary>
    private const string Prefab = """
<Prefab>
  <Window>
    <Widget>
      <Children>
        <FastPathWidget Id="Anchor" Label="@Anchor" />
        <FastPathWidget Id="Bool" Flag="@BoolValue" />
        <FastPathWidget Id="Counted" CountedFlag="@BoolValue" />
        <FastPathWidget Id="Int" Number="@IntValue" />
        <FastPathWidget Id="Widened" Ratio="@IntValue" />
        <FastPathWidget Id="Dotted" Branch.Margin="@IntValue" />
        <FastPathWidget Id="Boxed" Box.Inset="@IntValue" />
        <FastPathWidget Id="Surface" Surface.Depth="@IntValue" />
        <FastPathWidget Id="Float" Ratio="@FloatValue" />
        <FastPathWidget Id="UInt" Unsigned="@UIntValue" />
        <FastPathWidget Id="Color" Tint="@ColorValue" />
        <FastPathWidget Id="Double" Precise="@DoubleValue" />
        <FastPathWidget Id="Vec2" Offset="@Vec2Value" />
        <FastPathWidget Id="Label" Label="@TextValue" />
        <FastPathWidget Id="Picture" Picture="@TextValue" />
        <FastPathWidget Id="Mode" Mode="@ModeValue" />
        <FastPathWidget Id="StringToInt" Number="@TextNumberValue" />
        <FastPathWidget Id="Nullable" MaybeFlag="@BoolValue" />
        <FastPathWidget Id="Absent" NoSuchWidgetProperty="@IntValue" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""";

    private PrefabWorkspace _workspace = null!;
    private TestUIContext _ui = null!;
    private IDynamicMemberHost? _previousHost;


    [SetUp]
    public void SetUp()
    {
        _previousHost = DynamicMember.Host;
        DynamicMember.Host = new DynamicMemberHost();
        _workspace = new PrefabWorkspace((Movie, Prefab));
        _ui = new TestUIContext(_workspace.ResourceDepot, nameof(TypedPayloadFastPathTests));
    }

    [TearDown]
    public void TearDown()
    {
        DynamicMember.Host = _previousHost;
        _ui.Dispose();
        _workspace.Dispose();
    }

    private CompiledMovie Build() => CompiledMovie.Build(_workspace, Movie, typeof(FastPathRootVM), _ui);

    /// <summary>
    /// Instantiates and initializes a compiled movie with non-default initial values to verify active assignments.
    /// </summary>
    private (CompiledMovie Movie, FastPathSourceVM Source) Started()
    {
        var movie = Build();
        var source = new FastPathSourceVM();
        source.SetAll(true, 3, 0.5f, 7u, Color.White, 2.5d, new Vec2(1f, 2f), "text", "11", FastPathMode.Second);
        movie.SetDataSource(source);
        return (movie, source);
    }

    /// <summary>
    /// Evaluates the property value produced by standard reflection via <c>WidgetExtensions.SetWidgetAttribute</c> on a fresh widget instance.
    /// </summary>
    private object? Loader(string widgetProperty, object? value)
    {
        var reference = new FastPathWidget(_ui.Context);
        WidgetExtensions.SetWidgetAttribute(_ui.Context, reference, widgetProperty, value);
        return Read(reference, widgetProperty);
    }

    /// <summary>
    /// Traverses and retrieves property values along dotted paths using reflection matching loader traversal.
    /// </summary>
    private static object? Read(object? target, string name)
    {
        foreach (var segment in name.Split('.'))
        {
            var property = target?.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException($"'{name}' has no segment '{segment}'.");
            target = property.GetGetMethod()!.Invoke(target, []);
        }
        return target;
    }

    private void AssertMatchesLoader(CompiledMovie movie, string id, string widgetProperty, object? value) =>
        Assert.That(Read(movie.ById(id), widgetProperty), Is.EqualTo(Loader(widgetProperty, value)), $"{id}.{widgetProperty}");

    // --- Strongly typed payload variants ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that strongly typed property change notifications deliver payloads directly to widget properties
    /// without re-evaluating the getter on the ViewModel.
    /// </summary>
    [Test]
    public void EveryTypedPayloadVariant_ReachesItsWidget_WithoutRereadingTheProperty()
    {
        var (movie, source) = Started();

        source.AnnounceBool(false);
        source.AnnounceInt(41);
        source.AnnounceFloat(1.25f);
        source.AnnounceUInt(9u);
        source.AnnounceColor(Color.Black);
        source.AnnounceDouble(4.5d);
        source.AnnounceVec2(new Vec2(3f, 4f));

        AssertMatchesLoader(movie, "Bool", "Flag", false);
        AssertMatchesLoader(movie, "Int", "Number", 41);
        AssertMatchesLoader(movie, "Widened", "Ratio", 41);
        AssertMatchesLoader(movie, "Float", "Ratio", 1.25f);
        AssertMatchesLoader(movie, "UInt", "Unsigned", 9u);
        AssertMatchesLoader(movie, "Color", "Tint", Color.Black);
        AssertMatchesLoader(movie, "Double", "Precise", 4.5d);
        AssertMatchesLoader(movie, "Vec2", "Offset", new Vec2(3f, 4f));
        Assert.That(source.IntValue, Is.EqualTo(3), "the properties themselves never moved");
    }

    /// <summary>
    /// Verifies that numeric widening conversions (such as assigning an integer payload to a single-precision float property)
    /// execute consistently with GauntletUI binder semantics.
    /// </summary>
    [Test]
    public void AWidenedPayload_LandsTheWayTheBinderWouldHaveWidenedIt()
    {
        var (movie, source) = Started();

        source.AnnounceInt(41);

        AssertMatchesLoader(movie, "Widened", "Ratio", 41);
        Assert.That(((FastPathWidget) movie.ById("Widened")).Ratio, Is.EqualTo(41f));
    }

    /// <summary>
    /// Verifies that incompatible numeric narrowing assignments (such as double to int) throw the same exceptions
    /// as the XML loader without silently truncating values.
    /// </summary>
    [Test]
    public void ARejectedPayload_FailsTheWayTheLoaderFails()
    {
        var (movie, source) = SingleBindingMovie("Number", "DoubleValue");

        var raised = Assert.Catch(() => movie.SetDataSource(source))!;
        var loader = Assert.Catch(() => Loader("Number", 0d))!;

        // Unwraps the reflection TargetInvocationException generated by test harness execution.
        Assert.That(raised.InnerException!.GetType(), Is.EqualTo(loader.GetType()));
        Assert.That(((FastPathWidget) movie.ById("Target")).Number, Is.Zero, "and nothing was truncated into it");
    }

    /// <summary>
    /// Value-typed property notification delegates for each supported primitive payload type.
    /// </summary>
    private static readonly (string Variant, object Value, Action<ViewModel, string> Announce)[] TypedPayloads =
    [
        ("Bool", false, (x, name) => x.OnPropertyChangedWithValue(false, name)),
        ("Int", 41, (x, name) => x.OnPropertyChangedWithValue(41, name)),
        ("Float", 1.25f, (x, name) => x.OnPropertyChangedWithValue(1.25f, name)),
        ("UInt", 9u, (x, name) => x.OnPropertyChangedWithValue(9u, name)),
        ("Color", Color.Black, (x, name) => x.OnPropertyChangedWithValue(Color.Black, name)),
        ("Double", 4.5d, (x, name) => x.OnPropertyChangedWithValue(4.5d, name)),
        ("Vec2", new Vec2(3f, 4f), (x, name) => x.OnPropertyChangedWithValue(new Vec2(3f, 4f), name)),
    ];

    /// <summary>
    /// Extracts all bound property names and target widget mappings from the prefab XML.
    /// </summary>
    private static IEnumerable<(string Name, List<(string Id, string Property)> Targets)> BoundNames() =>
        Regex.Matches(Prefab, @"Id=""(\w+)"" ([\w.]+)=""@(\w+)""").Cast<Match>()
            .GroupBy(x => x.Groups[3].Value)
            .Select(x => (x.Key, x.Select(y => (y.Groups[1].Value, y.Groups[2].Value)).ToList()));

    /// <summary>
    /// Exhaustively tests all typed notification variants across every bound property name, asserting parity with
    /// loader exceptions and widget state even when types mismatch.
    /// </summary>
    [Test]
    public void EveryTypedPayload_UnderEveryBoundName_LandsTheWayTheLoaderLandsIt()
    {
        var movie = Build();
        var differences = new List<string>();
        var compared = new Dictionary<string, int>();
        var throwsCompared = 0;
        foreach (var (variant, value, announce) in TypedPayloads)
        {
            compared[variant] = 0;
            foreach (var (name, targets) in BoundNames())
            {
                var source = new FastPathSourceVM();
                source.SetAll(true, 3, 0.5f, 7u, Color.White, 2.5d, new Vec2(1f, 2f), "text", "11", FastPathMode.Second);
                movie.SetDataSource(source);
                Exception? raised = null;
                try
                {
                    announce(source, name);
                }
                catch (Exception e)
                {
                    raised = e;
                }

                // Simulates sequential widget attribute assignments via the XML loader to determine expected exceptions.
                Exception? expectedThrow = null;
                foreach (var (id, property) in targets)
                {
                    object? expected;
                    try
                    {
                        expected = Loader(property, value);
                    }
                    catch (InvalidOperationException) when (!HasProperty(property))
                    {
                        continue; // Skips properties undeclared on the widget type.
                    }
                    catch (Exception e)
                    {
                        expectedThrow = e;
                        break;
                    }
                    compared[variant]++;
                    if (!Equals(Read(movie.ById(id), property), expected))
                        differences.Add($"{variant} under {name}: {id}.{property} is {Read(movie.ById(id), property)}, the loader leaves {expected}");
                }
                if (expectedThrow is not null)
                    throwsCompared++;
                if (raised?.GetType() != expectedThrow?.GetType())
                    differences.Add($"{variant} under {name}: threw {raised?.GetType().Name ?? "nothing"}, the loader throws {expectedThrow?.GetType().Name ?? "nothing"}");
            }
        }

        Assert.That(differences, Is.Empty);
        // Each payload fits at least its own widget, and most of them throw somewhere else, as the loader's call does
        Assert.That(compared.Where(x => x.Value == 0).Select(x => x.Key), Is.Empty, "a payload that reached no widget compared nothing");
        Assert.That(throwsCompared, Is.GreaterThan(0), "and the mismatches that throw were compared too");
    }

    private bool HasProperty(string property)
    {
        try
        {
            Read(new FastPathWidget(_ui.Context), property);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    // --- Boxed and untyped object payload tests ---------------------------------------------------------------------

    [Test]
    public void ABoxedPayloadOfTheWidgetPropertysType_TakesTheGuard()
    {
        var (movie, source) = Started();

        source.AnnounceObject(false, "BoolValue");

        AssertMatchesLoader(movie, "Bool", "Flag", false);
    }

    [Test]
    public void AStringPayload_KeepsTheLoadersConversions()
    {
        var (movie, source) = Started();

        source.AnnounceObject("22", "TextNumberValue");
        source.AnnounceObject("sprite name", "TextValue");

        AssertMatchesLoader(movie, "StringToInt", "Number", "22");
        AssertMatchesLoader(movie, "Label", "Label", "sprite name");
        AssertMatchesLoader(movie, "Picture", "Picture", "sprite name");
    }

    /// <summary>
    /// Verifies that boxed enum payloads assigned to numeric widget properties retain binder unwrapping semantics.
    /// </summary>
    [Test]
    public void ABoxedEnumAtANumericProperty_KeepsTheLoadersUnwrapping()
    {
        var (movie, source) = Started();

        source.AnnounceObject(FastPathMode.Second, "TextNumberValue");

        AssertMatchesLoader(movie, "StringToInt", "Number", FastPathMode.Second);
    }

    /// <summary>
    /// Verifies that boxed enum payloads targeting matching enum properties execute via the type guard path.
    /// </summary>
    [Test]
    public void ABoxedEnumAtItsOwnProperty_TakesTheGuard()
    {
        var (movie, source) = Started();

        source.AnnounceObject(FastPathMode.First, "ModeValue");

        AssertMatchesLoader(movie, "Mode", "Mode", FastPathMode.First);
    }

    /// <summary>
    /// Verifies that null payloads route through the binder fallback, resetting value types to default and references to null.
    /// </summary>
    [Test]
    public void ANullPayload_ReachesTheSetterTheWayTheBinderDeliversIt()
    {
        var (movie, source) = Started();
        Assert.That(((FastPathWidget) movie.ById("Bool")).Flag, Is.True, "test premise: it starts non-default");

        source.AnnounceObject(null, "BoolValue");
        source.AnnounceObject(null, "TextValue");

        AssertMatchesLoader(movie, "Bool", "Flag", null);
        Assert.That(((FastPathWidget) movie.ById("Bool")).Flag, Is.False, "a value-typed property takes the default");
        AssertMatchesLoader(movie, "Label", "Label", null);
        Assert.That(((FastPathWidget) movie.ById("Label")).Label, Is.Null);
    }

    /// <summary>
    /// Verifies that ViewModel or binding list payloads targeting scalar bindings bypass scalar guards and route to scope handlers.
    /// </summary>
    [Test]
    public void AViewModelPayload_IsStillLeftToTheRereadingHandler()
    {
        var (movie, source) = Started();

        Assert.That(() => source.AnnounceObject(new FastPathSourceVM(), "BoolValue"), Throws.Nothing);
        Assert.That(() => source.AnnounceObject(new MBBindingList<ListItemVM>(), "BoolValue"), Throws.Nothing);

        AssertMatchesLoader(movie, "Bool", "Flag", source.BoolValue);
    }

    // --- Initial assignment and property change notifications -------------------------------------------------------

    [Test]
    public void TheInitialAssignment_GoesThroughTheSameGuard()
    {
        var (movie, _) = Started();

        AssertMatchesLoader(movie, "Bool", "Flag", true);
        AssertMatchesLoader(movie, "Int", "Number", 3);
        AssertMatchesLoader(movie, "Widened", "Ratio", 3);
        AssertMatchesLoader(movie, "Label", "Label", "text");
        AssertMatchesLoader(movie, "Dotted", "Branch.Margin", 3);
        AssertMatchesLoader(movie, "Surface", "Surface.Depth", 3);
    }

    [Test]
    public void AnOrdinaryNotification_RereadsAndAssignsThroughTheGuard()
    {
        var (movie, source) = Started();

        source.SetAll(false, 8, 0.5f, 7u, Color.White, 2.5d, new Vec2(1f, 2f), "changed", "11", FastPathMode.Second);
        source.AnnouncePlain("BoolValue");
        source.AnnouncePlain("IntValue");
        source.AnnouncePlain("TextValue");

        AssertMatchesLoader(movie, "Bool", "Flag", false);
        AssertMatchesLoader(movie, "Int", "Number", 8);
        AssertMatchesLoader(movie, "Label", "Label", "changed");
    }

    /// <summary>
    /// Verifies that widget property setters execute exactly once per assignment across both guarded and fallback branches.
    /// </summary>
    [Test]
    public void TheSetterRunsOncePerAssignment_OnBothBranches()
    {
        var (movie, source) = Started();
        var counted = (FastPathWidget) movie.ById("Counted");
        var afterAssignment = counted.FlagWrites;

        source.AnnounceBool(false);
        Assert.That(counted.FlagWrites, Is.EqualTo(afterAssignment + 1), "the guard succeeded");

        source.AnnounceObject(null, "BoolValue");
        Assert.That(counted.FlagWrites, Is.EqualTo(afterAssignment + 2), "and the guard failed");

        source.AnnouncePlain("BoolValue");
        Assert.That(counted.FlagWrites, Is.EqualTo(afterAssignment + 3), "and the property was reread");
    }

    // --- Fallback reflection target tests ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that dotted paths retain reflection-based assignments, matching XML loader behaviors across classes,
    /// structs, and interface types.
    /// </summary>
    [Test]
    public void ADottedPath_BehavesTheWayTheLoaderLeavesIt()
    {
        var (movie, source) = Started();

        source.AnnounceInt(12);

        AssertMatchesLoader(movie, "Dotted", "Branch.Margin", 12);
        AssertMatchesLoader(movie, "Surface", "Surface.Depth", 12);
        AssertMatchesLoader(movie, "Boxed", "Box.Inset", 12);
        Assert.That(((FastPathWidget) movie.ById("Boxed")).Box.Inset, Is.Zero,
            "a struct intermediate is read out by value, so the loader writes to a copy and so does this");
    }

    [Test]
    public void ANullableTarget_KeepsReflectionAndStillTakesTheValue()
    {
        var (movie, source) = Started();

        source.AnnounceBool(false);

        AssertMatchesLoader(movie, "Nullable", "MaybeFlag", false);
    }

    [Test]
    public void AWidgetPropertyTheTypeDoesNotHave_DoesNothing()
    {
        var (movie, source) = Started();

        Assert.That(() => source.AnnounceInt(5), Throws.Nothing);
        Assert.That(movie.ById("Absent"), Is.Not.Null);
    }

    /// <summary>
    /// Verifies that data sources lacking any bound properties leave all widget properties at default without throwing.
    /// </summary>
    [Test]
    public void ADataSourceWithNothingOnIt_LeavesEveryWidgetAtItsDefault()
    {
        var movie = Build();

        Assert.That(() => movie.SetDataSource(new FastPathEmptyVM()), Throws.Nothing);
        Assert.That(((FastPathWidget) movie.ById("Bool")).Flag, Is.False);
        Assert.That(((FastPathWidget) movie.ById("Label")).Label, Is.Null);
    }

    // --- Per-binding cache tests ------------------------------------------------------------------------------------

    /// <summary>
    /// Verifies that late member registrations on dynamic data sources invalidate previous lookup misses and resolve on subsequent notifications.
    /// </summary>
    [Test]
    public void AMemberRegisteredAfterTheMovieIsAttached_IsPickedUpByTheNextNotification()
    {
        var movie = Build();
        var source = new FastPathEmptyVM();
        movie.SetDataSource(source);
        Assert.That(((FastPathWidget) movie.ById("Label")).Label, Is.Null, "test premise: it missed");

        source.AddProperty("TextValue", new Bannerlord.BUTR.Shared.Utils.WrappedPropertyInfo(
            typeof(LoaderRegisteredMemberSource).GetProperty(nameof(LoaderRegisteredMemberSource.Value))!,
            new LoaderRegisteredMemberSource("registered late")));
        source.AnnouncePlain("TextValue");

        AssertMatchesLoader(movie, "Label", "Label", "registered late");
    }

    /// <summary>
    /// Verifies that swapping data source instances resolves property accessors against each concrete runtime type independently.
    /// </summary>
    [Test]
    public void ADataSourceSwap_ResolvesAgainstEachInstance()
    {
        var movie = Build();
        var first = new FastPathSourceVM();
        first.SetAll(true, 3, 0.5f, 7u, Color.White, 2.5d, new Vec2(1f, 2f), "first", "11", FastPathMode.Second);
        var second = new FastPathOtherSourceVM();

        movie.SetDataSource(first);
        AssertMatchesLoader(movie, "Label", "Label", "first");

        movie.SetDataSource(second);
        AssertMatchesLoader(movie, "Label", "Label", second.TextValue);
        AssertMatchesLoader(movie, "Int", "Number", second.IntValue);
        AssertMatchesLoader(movie, "Widened", "Ratio", second.IntValue);

        movie.SetDataSource(first);
        AssertMatchesLoader(movie, "Label", "Label", "first");
        AssertMatchesLoader(movie, "Int", "Number", 3);
    }

    /// <summary>
    /// Verifies that <c>DestroyDataSource</c> releases references to data sources and associated mixin tables,
    /// allowing garbage collection of both.
    /// </summary>
    [Test]
    public void DestroyDataSource_ReleasesTheSourceAndEveryMixinItsTableHeld()
    {
        var movie = Build();
        var (source, selected, unrelated) = AttachAndForget(movie);
        Assert.That(selected.IsAlive, Is.True, "test premise: the movie is holding them");

        movie.DestroyDataSource();
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.That(source.IsAlive, Is.False, "the data source");
        Assert.That(selected.IsAlive, Is.False, "the mixin a binding selected");
        Assert.That(unrelated.IsAlive, Is.False, "and the one only its table reached");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Source, WeakReference Selected, WeakReference Unrelated) AttachAndForget(CompiledMovie movie)
    {
        var source = new FastPathEmptyVM();
        var selected = new LoaderRegisteredMemberSource("selected");
        var unrelated = new LoaderRegisteredMemberSource("unrelated");
        var property = typeof(LoaderRegisteredMemberSource).GetProperty(nameof(LoaderRegisteredMemberSource.Value))!;
        source.AddProperty("TextValue", new Bannerlord.BUTR.Shared.Utils.WrappedPropertyInfo(property, selected));
        source.AddProperty("Unrelated", new Bannerlord.BUTR.Shared.Utils.WrappedPropertyInfo(property, unrelated));

        movie.SetDataSource(source);

        return (new WeakReference(source), new WeakReference(selected), new WeakReference(unrelated));
    }

    // --- Exception handling in property setters ----------------------------------------------------------------------

    /// <summary>
    /// Verifies that exceptions thrown by property setters are wrapped in <see cref="TargetInvocationException"/>
    /// to preserve parity with reflection-based invocation.
    /// </summary>
    [Test]
    public void AThrowingSetter_ThrowsTheSameWrapperTheLoaderDoes()
    {
        var (movie, source) = SingleBindingMovie("ThrowingFlag", "BoolValue");
        movie.SetDataSource(source);

        var raised = Assert.Catch(() => source.AnnounceBool(true))!;
        var loader = Assert.Catch(() => Loader("ThrowingFlag", true))!;

        Assert.That(raised, Is.InstanceOf<TargetInvocationException>());
        Assert.That(raised.GetType(), Is.EqualTo(loader.GetType()));
        Assert.That(raised.InnerException!.GetType(), Is.EqualTo(loader.InnerException!.GetType()));
        Assert.That(raised.InnerException!.Message, Is.EqualTo(loader.InnerException!.Message));
    }

    /// <summary>
    /// Verifies that setters throwing <see cref="TargetInvocationException"/> are doubly wrapped, matching reflection behavior.
    /// </summary>
    [Test]
    public void ASetterThrowingTheWrapperType_IsWrappedAgainJustAsReflectionWrapsIt()
    {
        var (movie, source) = SingleBindingMovie("DoublyWrappedFlag", "BoolValue");
        movie.SetDataSource(source);

        var raised = Assert.Catch(() => source.AnnounceBool(true))!;
        var loader = Assert.Catch(() => Loader("DoublyWrappedFlag", true))!;

        Assert.That(raised, Is.InstanceOf<TargetInvocationException>());
        Assert.That(raised.InnerException, Is.InstanceOf<TargetInvocationException>());
        Assert.That(raised.InnerException!.InnerException!.GetType(), Is.EqualTo(loader.InnerException!.InnerException!.GetType()));
    }

    /// <summary>
    /// Verifies that widget properties lacking public setters fail identically to the XML loader rather than silently ignoring assignments.
    /// </summary>
    [Test]
    public void AWidgetPropertyWithNoPublicSetter_FailsTheWayTheLoaderFails()
    {
        var (movie, source) = SingleBindingMovie("ReadOnlyFlag", "BoolValue");

        var raised = Assert.Catch(() => movie.SetDataSource(source))!;
        var loader = Assert.Catch(() => Loader("ReadOnlyFlag", false))!;

        // Unwraps the outer reflection wrapper from test harness execution.
        Assert.That(raised.InnerException!.GetType(), Is.EqualTo(loader.GetType()));
    }

    /// <summary>
    /// Builds an isolated single-binding compiled movie and returns an unattached data source for initial assignment testing.
    /// </summary>
    private (CompiledMovie Movie, FastPathSourceVM Source) SingleBindingMovie(string widgetProperty, string sourcePath)
    {
        var name = "FastPathSingle" + widgetProperty;
        var prefab = $"""
<Prefab>
  <Window>
    <Widget>
      <Children>
        <FastPathWidget Id="Target" {widgetProperty}="@{sourcePath}" />
      </Children>
    </Widget>
  </Window>
</Prefab>
""";
        using var workspace = new PrefabWorkspace((name, prefab));
        var movie = CompiledMovie.Build(workspace, name, typeof(FastPathRootVM), _ui);
        return (movie, new FastPathSourceVM());
    }
}
