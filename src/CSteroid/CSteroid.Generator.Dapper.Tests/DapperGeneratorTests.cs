using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Testing;
using CSteroid.Generator.Generators;
using CSteroid.Generator.Dapper;
using Xunit;

namespace CSteroid.Tests
{
    public class DapperGeneratorTests
    {
        private static readonly IIncrementalGenerator[] Generators = { new CSteroidGenerator(), new CSteroidDapperGenerator() };

        private static Compilation RunGenerator(params string[] sources)
        {
            return GeneratorTestHarness.RunGenerator(Generators, sources);
        }

        [Fact]
        public void Dapper_PropagatesColumnAttribute()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.ComponentModel.DataAnnotations.Schema;

namespace System.ComponentModel.DataAnnotations.Schema
{
    public class ColumnAttribute : Attribute
    {
        public ColumnAttribute(string name) { }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.Dapper)]
public interface IUserModelDefinition
{
    public int Id { get; set; }

    [Column(""user_name"")]
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("using System.ComponentModel.DataAnnotations.Schema;", content);
            Assert.Contains("[Column(\"user_name\")]", content);
        }

        [Fact]
        public void Dapper_GeneratesConstructorOverload()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.Dapper)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("public User(int id, string name)", content);
            Assert.Contains("Id = id;", content);
            Assert.Contains("Name = name;", content);
        }

        [Fact]
        public void Dapper_DoesNotPropagateKeyAttribute()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.ComponentModel.DataAnnotations.Schema;

namespace System.ComponentModel.DataAnnotations.Schema
{
    public class KeyAttribute : Attribute { }
    public class ColumnAttribute : Attribute
    {
        public ColumnAttribute(string name) { }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.Dapper)]
public interface IUserModelDefinition
{
    [Key]
    public int Id { get; set; }

    [Column(""user_name"")]
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.DoesNotContain("[Key]", content);
            Assert.Contains("[Column(\"user_name\")]", content);
        }
    }
}
