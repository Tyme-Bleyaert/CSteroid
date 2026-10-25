using System;

namespace CSteroid
{
    /// <summary>
    /// Marks a property as the partition key for a Cosmos DB container.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class CosmosPartitionKeyAttribute : Attribute
    {
    }
}
