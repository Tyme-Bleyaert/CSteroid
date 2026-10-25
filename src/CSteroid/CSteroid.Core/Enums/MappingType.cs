using System;

namespace CSteroid
{
    [Flags]
    public enum MappingTypes
    {
        None = 0,
        ExtensionMethod = 1,
        Static = 2,
        ImplicitCast = 4,
        ExplicitCast = 8,
        DependencyInjection = 16,
        ProjectionExpression = 32,
    }
}
