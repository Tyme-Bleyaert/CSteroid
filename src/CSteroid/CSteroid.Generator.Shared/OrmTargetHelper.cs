using System.Collections.Generic;
using static CSteroid.Generator.Shared.GeneratorConstants;

namespace CSteroid.Generator.Shared
{
    public static class OrmTargetHelper
    {
        public static bool IsSingleOrmFlag(int ormTarget)
        {
            return ormTarget != 0 && (ormTarget & (ormTarget - 1)) == 0;
        }

        public static string GetOrmFlagNames(int ormTarget)
        {
            var names = new List<string>();
            if ((ormTarget & OrmTargetEFCore) != 0)
                names.Add("EFCore");
            if ((ormTarget & OrmTargetDapper) != 0)
                names.Add("Dapper");
            if ((ormTarget & OrmTargetCosmosDB) != 0)
                names.Add("CosmosDB");
            if ((ormTarget & OrmTargetMongoDB) != 0)
                names.Add("MongoDB");
            if ((ormTarget & OrmTargetPostgreSQL) != 0)
                names.Add("PostgreSQL");
            return string.Join(", ", names);
        }
    }
}
