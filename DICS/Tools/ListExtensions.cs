using System;
using System.Collections.Generic;
using System.Text;

namespace DICS
{
    public static class EnumerableExtensions
    {
        public static string NiceList<T>(this IEnumerable<T> list, string prefix = "- ")
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));

            var result = new StringBuilder();
            foreach (var item in list) result.AppendLine($"{prefix}{item}");

            return result.ToString().TrimEnd();
        }

        public static string Join<T>(this IEnumerable<T> list, string separator)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (separator == null) throw new ArgumentNullException(nameof(separator));

            return string.Join(separator, list);
        }

        public static string JoinC<T>(this IEnumerable<T> list)
        {
            return Join(list, ",");
        }

        public static string JoinCS<T>(this IEnumerable<T> list)
        {
            return Join(list, ", ");
        }
    }
}