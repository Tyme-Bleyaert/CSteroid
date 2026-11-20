using System;

namespace CSteroid.Generator.AutoMediator
{
    internal static class StableName
    {
        internal static string Hash(string value)
        {
            unchecked
            {
                var hash = 14695981039346656037UL;
                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= 1099511628211UL;
                }
                return hash.ToString("x16");
            }
        }
    }
}
