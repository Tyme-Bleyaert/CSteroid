using System;

namespace CSteroid
{
    /// <summary>
    /// Marks a property to be mapped as a PostgreSQL <c>jsonb</c> column
    /// when the PostgreSQL ORM target is selected.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class PostgreJsonbAttribute : Attribute
    {
    }
}
