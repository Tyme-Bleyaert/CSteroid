using System;
using System.Text;

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

        internal static string HintName(string metadataIdentity, string routeKey)
        {
            var builder = new StringBuilder("AutoMediator.");
            var length = Math.Min(metadataIdentity.Length, 80);
            for (int i = 0; i < length; i++)
            {
                var character = metadataIdentity[i];
                builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
            }
            return builder.Append('.').Append(Hash(routeKey)).Append(".g.cs").ToString();
        }
    }
}
