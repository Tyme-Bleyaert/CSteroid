using System;

namespace CSteroid
{
    [Flags]
    public enum OrmTarget
    {
        None = 0,
        EFCore = 1,
        Dapper = 2,
        CosmosDB = 4,
        MongoDB = 8,
        PostgreSQL = 16,
    }
}
