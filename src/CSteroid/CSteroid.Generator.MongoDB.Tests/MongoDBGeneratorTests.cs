using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Testing;
using CSteroid.Generator.Generators;
using CSteroid.Generator.MongoDB;
using Xunit;

namespace CSteroid.Tests
{
    public class MongoDBGeneratorTests
    {
        private static readonly IIncrementalGenerator[] Generators = { new CSteroidGenerator(), new CSteroidMongoDBGenerator() };

        private static Compilation RunGenerator(params string[] sources)
        {
            return GeneratorTestHarness.RunGenerator(Generators, sources);
        }

        [Fact]
        public void MongoDB_GeneratesBsonIdAndBsonElementAttributes()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoDB.Bson.Serialization.Attributes
{
    using System;
    public class BsonIdAttribute : Attribute { }
    public class BsonElementAttribute : Attribute
    {
        public BsonElementAttribute(string name) { }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.MongoDB)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string FirstName { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("using MongoDB.Bson.Serialization.Attributes;", content);
            Assert.Contains("[BsonId]", content);
            Assert.Contains("[BsonElement(\"id\")]", content);
            Assert.Contains("[BsonElement(\"firstName\")]", content);
        }

        [Fact]
        public void MongoDB_KeyPropertyNamedOtherThanId_StillUsesBsonId()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.ComponentModel.DataAnnotations.Schema;
using MongoDB.Bson.Serialization.Attributes;

namespace System.ComponentModel.DataAnnotations.Schema
{
    public class KeyAttribute : Attribute { }
}

namespace MongoDB.Bson.Serialization.Attributes
{
    using System;
    public class BsonIdAttribute : Attribute { }
    public class BsonElementAttribute : Attribute
    {
        public BsonElementAttribute(string name) { }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.MongoDB)]
public interface IUserModelDefinition
{
    [Key]
    public int UserId { get; set; }
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("[BsonId]", content);
            Assert.Contains("[BsonElement(\"userId\")]", content);
            Assert.Contains("[BsonElement(\"name\")]", content);
        }
    }
}
