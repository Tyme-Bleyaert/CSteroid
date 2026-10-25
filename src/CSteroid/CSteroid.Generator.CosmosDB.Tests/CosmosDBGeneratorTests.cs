using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Testing;
using CSteroid.Generator.Generators;
using CSteroid.Generator.CosmosDB;
using Xunit;

namespace CSteroid.Tests
{
    public class CosmosDBGeneratorTests
    {
        private static readonly IIncrementalGenerator[] Generators = { new CSteroidGenerator(), new CSteroidCosmosDBGenerator() };

        private static Compilation RunGenerator(params string[] sources)
        {
            return GeneratorTestHarness.RunGenerator(Generators, sources);
        }

        [Fact]
        public void CosmosDB_GeneratesJsonPropertyNameAttributes()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.Text.Json.Serialization;

namespace System.Text.Json.Serialization
{
    public class JsonPropertyNameAttribute : Attribute
    {
        public JsonPropertyNameAttribute(string name) { }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.CosmosDB)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string FirstName { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("using System.Text.Json.Serialization;", content);
            Assert.Contains("[JsonPropertyName(\"id\")]", content);
            Assert.Contains("[JsonPropertyName(\"firstName\")]", content);
        }

        [Fact]
        public void CosmosDB_KeyPropertyNamedOtherThanId_StillUsesIdJsonName()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace System.ComponentModel.DataAnnotations.Schema
{
    public class KeyAttribute : Attribute { }
}

namespace System.Text.Json.Serialization
{
    public class JsonPropertyNameAttribute : Attribute
    {
        public JsonPropertyNameAttribute(string name) { }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.CosmosDB)]
public interface IUserModelDefinition
{
    [Key]
    public int UserId { get; set; }
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("[JsonPropertyName(\"id\")]", content);
            Assert.Contains("[JsonPropertyName(\"name\")]", content);
        }

        [Fact]
        public void CosmosDB_PartitionKey_EmitsAttribute()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.Text.Json.Serialization;

namespace System.Text.Json.Serialization
{
    public class JsonPropertyNameAttribute : Attribute
    {
        public JsonPropertyNameAttribute(string name) { }
    }
}

[CanonicalModelAttribute(""User"", PartitionKey: ""Category"")]
[OrmTarget(OrmTarget.CosmosDB)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Category { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("[global::CSteroid.CosmosPartitionKey]", content);
            Assert.Contains("[JsonPropertyName(\"category\")]", content);
        }
    }
}
