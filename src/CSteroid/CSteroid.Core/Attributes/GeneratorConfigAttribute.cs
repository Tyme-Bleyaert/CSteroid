using System;

namespace CSteroid
{
    /// <summary>
    /// Assembly-level configuration for the CSteroid source generator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class GeneratorConfigAttribute : Attribute
    {
        public FileNamingConvention FileNamingConvention { get; set; }
        public string? ValidationProject { get; set; }
        public string? MappingProject { get; set; }

        /// <summary>
        /// When set, only models whose generated namespace is listed here are emitted by this
        /// project. Use the "metadata lib" pattern: keep the [CanonicalModel] interfaces in a
        /// shared library and let each consuming project generate only the models it owns.
        /// Without this, every referencing project generates every model (CS0436 shadowing).
        /// </summary>
        public string[]? GenerateOnlyNamespaces { get; set; }
    }
}
