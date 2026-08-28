using System;
using System.Collections.Generic;

namespace DICS
{
    public static class CollectionOps
    {
        public static IDictionary<K, ISet<T>> IndexBy<T, K>(this IEnumerable<T> enumerable, Func<T, K> selector)
        {
            Dictionary<K, ISet<T>> indexed = new();
            foreach (var b in enumerable)
            {
                var key = selector(b);
                if (!indexed.ContainsKey(key)) indexed[key] = new HashSet<T>();
                indexed[key].Add(b);
            }

            return indexed;
        }

        public static Dictionary<TKey, ISet<TValue>> MergeDictionaries<TKey, TValue>(
            Dictionary<TKey, ISet<TValue>> first,
            IDictionary<TKey, ISet<TValue>> second)
        {
            // Initialize the result with the contents of the first dictionary
            var result = new Dictionary<TKey, ISet<TValue>>();

            foreach (var kvp in first)
                // Create a new set so we don't modify the original
                result[kvp.Key] = new HashSet<TValue>(kvp.Value);

            // Merge contents from the second dictionary
            foreach (var kvp in second)
                if (result.TryGetValue(kvp.Key, out var existingSet))
                    // If the key exists, union the sets
                    existingSet.UnionWith(kvp.Value);
                else
                    // Otherwise, add a new set for this key
                    result[kvp.Key] = new HashSet<TValue>(kvp.Value);

            return result;
        }
    }
}