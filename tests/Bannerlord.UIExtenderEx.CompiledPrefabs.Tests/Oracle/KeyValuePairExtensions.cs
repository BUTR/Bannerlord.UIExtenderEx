using System.Collections.Generic;

namespace Bannerlord.UIExtenderEx.Tests.CompiledPrefabs.Oracle;

/// <summary>
/// Provides tuple deconstruction for <see cref="KeyValuePair{TKey, TValue}"/> instances on .NET Framework.
/// </summary>
internal static class KeyValuePairExtensions
{
    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }
}
