using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Tests.CodeGenerator;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs;

/// <summary>
/// Defines the declared root ViewModel against which the fast-path prefab is compiled. Exposes the single resolvable anchor property while omitting target bound members, forcing dynamic name-based binding resolution.
/// </summary>
public class FastPathRootVM : ViewModel
{
    [DataSourceProperty]
    public string Anchor => "anchor";
}

/// <summary>
/// Provides the runtime ViewModel instance supplied to the prefab fixture. Implements property payload variants raised with values distinct from property getters to differentiate direct payload consumption from getter re-evaluations.
/// </summary>
public class FastPathSourceVM : FastPathRootVM
{
    [DataSourceProperty]
    public bool BoolValue { get; private set; }

    [DataSourceProperty]
    public int IntValue { get; private set; }

    [DataSourceProperty]
    public float FloatValue { get; private set; }

    [DataSourceProperty]
    public uint UIntValue { get; private set; }

    [DataSourceProperty]
    public Color ColorValue { get; private set; } = Color.Black;

    [DataSourceProperty]
    public double DoubleValue { get; private set; }

    [DataSourceProperty]
    public Vec2 Vec2Value { get; private set; }

    [DataSourceProperty]
    public string? TextValue { get; private set; }

    /// <summary>Provides a string value bound to an integer widget property, requiring <c>ConvertObject</c> coercion.</summary>
    [DataSourceProperty]
    public string TextNumberValue { get; private set; } = "0";

    [DataSourceProperty]
    public FastPathMode ModeValue { get; private set; }

    /// <summary>Updates all properties and triggers standard property change notifications, causing bindings to reread backing values.</summary>
    public void SetAll(bool flag, int number, float ratio, uint unsigned, Color tint, double precise, Vec2 offset, string? text, string textNumber, FastPathMode mode)
    {
        BoolValue = flag;
        IntValue = number;
        FloatValue = ratio;
        UIntValue = unsigned;
        ColorValue = tint;
        DoubleValue = precise;
        Vec2Value = offset;
        TextValue = text;
        TextNumberValue = textNumber;
        ModeValue = mode;
    }

    public void AnnouncePlain(string propertyName) => OnPropertyChanged(propertyName);

    public void AnnounceBool(bool value) => OnPropertyChangedWithValue(value, nameof(BoolValue));

    public void AnnounceInt(int value) => OnPropertyChangedWithValue(value, nameof(IntValue));

    public void AnnounceFloat(float value) => OnPropertyChangedWithValue(value, nameof(FloatValue));

    public void AnnounceUInt(uint value) => OnPropertyChangedWithValue(value, nameof(UIntValue));

    public void AnnounceColor(Color value) => OnPropertyChangedWithValue(value, nameof(ColorValue));

    public void AnnounceDouble(double value) => OnPropertyChangedWithValue(value, nameof(DoubleValue));

    public void AnnounceVec2(Vec2 value) => OnPropertyChangedWithValue(value, nameof(Vec2Value));

    /// <summary>
    /// Dispatches object change notifications for reference-typed properties, boxed values, strings, or null values through the guarded branch.
    /// </summary>
    public void AnnounceObject(object? value, string propertyName) => OnPropertyChangedWithValue(value!, propertyName);
}

/// <summary>Represents an empty data source omitting bound properties, causing dynamic name-based bindings to miss.</summary>
public class FastPathEmptyVM : FastPathRootVM
{
    /// <summary>Notifies that a property changed on an unregistered member to verify late registration observation.</summary>
    public void AnnouncePlain(string propertyName) => OnPropertyChanged(propertyName);
}

/// <summary>
/// Defines the declared root ViewModel for dynamic name-based benchmarks, omitting bound properties to force runtime member resolution against the instance.
/// </summary>
public class BenchRootVM : ViewModel
{
    [DataSourceProperty]
    public string Anchor => "anchor";
}

/// <summary>
/// Represents the concrete runtime ViewModel instance for benchmarks, allowing static compile-time binding resolution to isolate emission overhead from event dispatch.
/// </summary>
public class BenchSourceVM : BenchRootVM
{
    [DataSourceProperty]
    public bool Flag { get; set; }

    /// <summary>
    /// Represents a float property bound to a float widget attribute with integer change payloads to test binder type widening.
    /// </summary>
    [DataSourceProperty]
    public float Ratio { get; set; }

    public void AnnounceFlag(bool value)
    {
        Flag = value;
        OnPropertyChangedWithValue(value, nameof(Flag));
    }

    /// <summary>Dispatches an integer property change payload for a float property, verifying typed widening casts.</summary>
    public void AnnounceRatioAsInt(int value)
    {
        Ratio = value;
        OnPropertyChangedWithValue(value, nameof(Ratio));
    }

    /// <summary>Dispatches a boxed property change payload for a float property, verifying fallback binder widening.</summary>
    public void AnnounceRatioAsObject(int value)
    {
        Ratio = value;
        OnPropertyChangedWithValue<object>(value, nameof(Ratio));
    }

    public void SetFlagAndAnnouncePlainly(bool value)
    {
        Flag = value;
        OnPropertyChanged(nameof(Flag));
    }
}

/// <summary>
/// Provides an alternative runtime ViewModel implementation sharing property names with divergent types to test polymorphism and rebinding.
/// </summary>
public class FastPathOtherSourceVM : FastPathRootVM
{
    [DataSourceProperty]
    public string TextValue => "other";

    /// <summary>Defines an integer property sharing a name with <see cref="FastPathSourceVM.IntValue"/> but declared as <see cref="short"/>.</summary>
    [DataSourceProperty]
    public short IntValue => 6;
}

/// <summary>Represents a benchmark data source containing none of the bound property names, verifying lookup miss handling.</summary>
public class BenchEmptyVM : BenchRootVM
{
    public void AnnouncePlain(string propertyName) => OnPropertyChanged(propertyName);
}