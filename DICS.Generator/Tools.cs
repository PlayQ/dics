using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DICS.Generator
{
    // we cannot reference DICS here, so we have to copypaste
    public static class Tools
    {
        public static string Join<T>(this IEnumerable<T> list, string separator)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (separator == null) throw new ArgumentNullException(nameof(separator));

            return string.Join(separator, list);
        }

        public static string Shift(this string input, int spaces)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (spaces < 0) throw new ArgumentOutOfRangeException(nameof(spaces), "Spaces must be non-negative.");

            var indentation = new string(' ', spaces);
            var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            return string.Join("\n", lines.Select(line => indentation + line));
        }

        public static string InNs(this string input, string? ns)
        {
            if (string.IsNullOrEmpty(ns)) return input;

            var ret = $@"namespace {ns}
{{
    {input.Shift(4).Trim()}
}}";
            return ret;
        }

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

        public static string NiceList<T>(this IEnumerable<T> list, string prefix = "- ")
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));

            var result = new StringBuilder();
            foreach (var item in list) result.AppendLine($"{prefix}{item}");

            return result.ToString().TrimEnd();
        }
    }
}