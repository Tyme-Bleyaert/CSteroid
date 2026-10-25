using System;

namespace CSteroid
{
    [Flags]
    public enum ValidationType
    {
        None = 0,
        DataAnnotations = 1,
        Custom = 2,
        Async = 4,
    }
}
