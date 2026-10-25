using System;

namespace CSteroid
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ExcludeFromGenerationAttribute : Attribute
    {
        public string[]? ExcludedModels { get; set; }

        public ExcludeFromGenerationAttribute()
        {
        }

        public ExcludeFromGenerationAttribute(params string[] namesOfModelsForExclusion)
        {
            ExcludedModels = namesOfModelsForExclusion;
        }
    }
}
