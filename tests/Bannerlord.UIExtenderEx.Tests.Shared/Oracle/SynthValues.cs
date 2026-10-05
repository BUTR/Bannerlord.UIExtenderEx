using System;
using System.Collections.Generic;
using System.Globalization;

using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Tests.Oracle;

/// <summary>
/// Generates deterministic synthetic property values for test ViewModels computed as pure functions of an instance seed, property identifier, and mutation round.
/// Guarantees value parity across multiple ViewModel instances evaluated during oracle passes.
/// </summary>
public static class SynthValues
{
    /// <summary>Records property setter invocations and lifecycle events across ViewModels within a synthetic hierarchy.</summary>
    public sealed class Log
    {
        public List<string> Entries { get; } = [];

        public void Add(string entry) => Entries.Add(entry);
    }

    /// <summary>Specifies the underlying concrete type represented by an <see cref="object"/>-typed property in synthetic ViewModels.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class HoldsAttribute(Type type) : Attribute
    {
        public Type Type { get; } = type;
    }

    /// <summary>Resolves the concrete value type assigned to a synthetic ViewModel property, checking for <see cref="HoldsAttribute"/>.</summary>
    public static Type ValueTypeOf(System.Reflection.PropertyInfo property) =>
        ((HoldsAttribute?) Attribute.GetCustomAttribute(property, typeof(HoldsAttribute)))?.Type ?? property.PropertyType;

    public static T Get<T>(int seed, string name, int round) => (T) Get(typeof(T), seed, name, round);

    public static object Get(Type type, int seed, string name, int round)
    {
        var hash = Hash(seed, name, round);
        if (type == typeof(bool))
            return (hash & 1) == 0;
        // Enforces Min < Max ordering for numeric range properties to prevent slider infinite clamp loops.
        if (IsBound(name, "Min") && (type == typeof(int) || type == typeof(uint) || type == typeof(float) || type == typeof(double)))
            return Convert.ChangeType(hash % 10, type, CultureInfo.InvariantCulture);
        if (IsBound(name, "Max") && (type == typeof(int) || type == typeof(uint) || type == typeof(float) || type == typeof(double)))
            return Convert.ChangeType(150 + hash % 10, type, CultureInfo.InvariantCulture);
        if (type == typeof(int))
            return hash % 200 - 20;
        if (type == typeof(uint))
            return (uint) (hash % 200);
        if (type == typeof(float))
            return hash % 1000 / 10f;
        if (type == typeof(double))
            return hash % 1000 / 7d;
        // Formats color strings with hex syntax to ensure GauntletUI color parser compatibility.
        if (type == typeof(string) && name.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0)
            return "#" + ((uint) hash | 0xFFu).ToString("X8", CultureInfo.InvariantCulture);
        if (type == typeof(string))
            return $"{name}-{seed}-{round}";
        if (type == typeof(Color))
            return Color.FromUint((uint) hash | 0xFF000000u);
        if (type == typeof(Vec2))
            return new Vec2(hash % 300, hash / 300 % 300);
        if (type == typeof(System.Numerics.Vector2))
            return new System.Numerics.Vector2(hash % 300, hash / 300 % 300);
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.GetValue(hash % values.Length)!;
        }
        throw new NotSupportedException($"No synthesized value for {type}.");
    }

    public static string Describe(object? value) => value switch
    {
        null => "null",
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        ViewModel viewModel => viewModel.GetType().Name,
        _ => value.ToString() ?? "",
    };

    /// <summary>Computes a deterministic 32-bit FNV-1a hash across the seed, property name, and round counter.</summary>
    /// <summary>Matches boundary property prefixes (such as <c>Min</c>, <c>MinValue</c>, <c>Max</c>) at word boundaries.</summary>
    private static bool IsBound(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.Ordinal) && (name.Length == prefix.Length || char.IsUpper(name[prefix.Length]));

    internal static int Hash(int seed, string name, int round)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in $"{seed}|{name}|{round}")
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return (int) (hash & 0x7FFFFFFF);
        }
    }
}