using System;
using System.Linq;

namespace DICS
{
    public static class StringExtensions
    {
        public static string Shift(this string input, int spaces)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (spaces < 0) throw new ArgumentOutOfRangeException(nameof(spaces), "Spaces must be non-negative.");

            var indentation = new string(' ', spaces);
            var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            return string.Join("\n", lines.Select(line => indentation + line));
        }
    }
}