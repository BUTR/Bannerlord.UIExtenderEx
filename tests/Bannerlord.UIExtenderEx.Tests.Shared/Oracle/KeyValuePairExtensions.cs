using System.Collections.Generic;

namespace Bannerlord.UIExtenderEx.Tests.Oracle;

/// <summary>Provides deconstruction support for <see cref="KeyValuePair{TKey, TValue}"/> instances on runtime targets lacking native tuple deconstructors.</summary>
internal static class KeyValuePairExtensions
{
    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }
}