using System;

namespace CSteroid
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
    public sealed class CanonicalModelAttribute : Attribute
    {
        public string Target { get; set; }
        public ValidationType ValidationType { get; set; }
        public MappingTypes MappingTypes { get; set; }
        public DeclarationType DeclarationType { get; set; }
        public string? Version { get; set; }
        public string[]? ExcludedProperties { get; set; }
        public string? PartitionKey { get; set; }
        public string? CustomNamespace { get; set; }
        public string? FolderPath { get; set; }

        /// <summary>
        /// When <c>true</c>, generated mapping methods using this model as a mapping *source*
        /// are branch-free and non-overridable: no <c>ConfigureMapping</c> hook, no per-call
        /// override check, and no mapper class unless DI registration or a projection
        /// expression needs one — each entry point inlines its own copy of the mapping.
        /// Use for hot paths where custom mapping overrides are not needed.
        /// </summary>
        public bool Simple { get; set; }

        /// <summary>
        /// When <c>true</c>, every attribute on the interface's properties is forwarded to this
        /// generated model's properties — not just DataAnnotations/schema annotations. Use for
        /// ORM-specific attributes (e.g. EF Core <c>[Precision]</c>, <c>[Comment]</c>) or custom
        /// attributes that must exist on the generated type. Only set this on targets whose
        /// generated project references the attribute's assembly (e.g. an EF Core entity target,
        /// never a plain DTO target — the DTO's project may not reference EF Core).
        /// </summary>
        public bool ForwardPropertyAttributes { get; set; }

        public CanonicalModelAttribute(string target)
        {
            Target = target;
        }

        public CanonicalModelAttribute(string target,
                                       ValidationType ValidationType = default,
                                       MappingTypes MappingTypes = default,
                                       DeclarationType DeclarationType = default,
                                       string? Version = null,
                                       string[]? ExcludedProperties = null,
                                       string? PartitionKey = null)
        {
            Target = target;
            this.ValidationType = ValidationType;
            this.MappingTypes = MappingTypes;
            this.DeclarationType = DeclarationType;
            this.Version = Version;
            this.ExcludedProperties = ExcludedProperties;
            this.PartitionKey = PartitionKey;
        }
    }
}
