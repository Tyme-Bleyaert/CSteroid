using System;

namespace CSteroid
{
    /// <summary>
    /// Selects the ORM target for models generated from the decorated interface.
    /// Apply alongside <see cref="CanonicalModelAttribute"/> and reference the matching
    /// generator package (e.g. CSteroid.Generator.EFCore) — that package emits the model.
    /// Only one ORM target may apply to a given model; combining targets is an error (CSG101).
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
    public sealed class OrmTargetAttribute : Attribute
    {
        public OrmTarget Target { get; }

        /// <summary>
        /// Optional name of the generated model this target applies to.
        /// When null, the target applies to every model generated from the interface.
        /// </summary>
        public string? Model { get; set; }

        public OrmTargetAttribute(OrmTarget target)
        {
            Target = target;
        }
    }
}
