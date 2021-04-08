using System;
using System.Runtime.CompilerServices;

namespace Bannerlord.UIExtenderEx.Extensions
{
    internal static class ConditionalWeakTableExtensions
    {
        public static TValue GetOrAdd<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> table, TKey key, Func<TKey, TValue> valueFactory)
            where TKey : class
            where TValue : class
        {
            return table.GetValue(key, k => valueFactory(k));
        }
    }
}