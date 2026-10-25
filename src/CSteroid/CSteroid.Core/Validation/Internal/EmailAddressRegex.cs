using System.Text.RegularExpressions;

namespace CSteroid
{
    internal static class EmailAddressRegex
    {
        private static readonly Regex Regex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled);

        public static bool IsMatch(string value) => Regex.IsMatch(value);
    }
}
