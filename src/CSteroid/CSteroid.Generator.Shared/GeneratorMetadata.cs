using System;
using System.Linq;
using System.Reflection;

namespace CSteroid.Generator.Shared
{
    /// <summary>
    /// Identity of the generator assembly, stamped onto every generated type via
    /// [GeneratedCode] so analyzers, coverage tools, and IDEs can tell the code
    /// is machine-written. Pure metadata: the JIT ignores it and codegen is unchanged.
    /// </summary>
    public static class GeneratorMetadata
    {
        public static readonly string ToolName = typeof(GeneratorMetadata).Assembly.GetName().Name;

        public static readonly string Version = GetVersion();

        /// <summary>Full attribute line, e.g. [global::System.CodeDom.Compiler.GeneratedCode("CSteroid.Generator.Shared", "1.0.0.0")]</summary>
        public static readonly string AttributeLine =
            $"[global::System.CodeDom.Compiler.GeneratedCode(\"{ToolName}\", \"{Version}\")]";

        private static string GetVersion()
        {
            var assembly = typeof(GeneratorMetadata).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(informational))
            {
                // Drop the commit-hash suffix ("1.2.3+abc123" -> "1.2.3").
                int plusIndex = informational.IndexOf('+');
                return plusIndex >= 0 ? informational.Substring(0, plusIndex) : informational;
            }

            return assembly.GetName().Version?.ToString() ?? "0.0.0.0";
        }
    }
}
