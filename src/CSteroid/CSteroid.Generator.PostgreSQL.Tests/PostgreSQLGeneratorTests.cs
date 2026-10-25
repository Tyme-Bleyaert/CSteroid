using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Testing;
using CSteroid.Generator.Generators;
using CSteroid.Generator.PostgreSQL;
using Xunit;

namespace CSteroid.Tests
{
    public class PostgreSQLGeneratorTests
    {
        private static readonly IIncrementalGenerator[] Generators = { new CSteroidGenerator(), new CSteroidPostgreSQLGenerator() };

        private static Compilation RunGenerator(params string[] sources)
        {
            return GeneratorTestHarness.RunGenerator(Generators, sources);
        }

        [Fact]
        public void PostgreSQL_GeneratesSnakeCaseColumnAndIdentityKey()
        {
            var compilation = RunGenerator(@"
using CSteroid;

namespace System.ComponentModel.DataAnnotations
{
    public class DatabaseGeneratedAttribute : Attribute
    {
        public DatabaseGeneratedAttribute(DatabaseGeneratedOption option) { }
    }
    public enum DatabaseGeneratedOption { None, Identity, Computed }
}

namespace System.ComponentModel.DataAnnotations.Schema
{
    public class KeyAttribute : Attribute { }
    public class ColumnAttribute : Attribute
    {
        public ColumnAttribute(string name) { }
        public string TypeName { get; set; }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.PostgreSQL)]
public interface IUserModelDefinition
{
    [System.ComponentModel.DataAnnotations.Schema.Key]
    public int Id { get; set; }
    public string FirstName { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("using System.ComponentModel.DataAnnotations;", content);
            Assert.Contains("using System.ComponentModel.DataAnnotations.Schema;", content);
            Assert.Contains("[Key]", content);
            Assert.Contains("[DatabaseGenerated(DatabaseGeneratedOption.Identity)]", content);
            Assert.Contains("[Column(\"id\")]", content);
            Assert.Contains("[Column(\"first_name\")]", content);
        }

        [Fact]
        public void PostgreSQL_PropagatesExplicitColumn()
        {
            var compilation = RunGenerator(@"
using CSteroid;

namespace System.ComponentModel.DataAnnotations.Schema
{
    public class ColumnAttribute : Attribute
    {
        public ColumnAttribute(string name) { }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.PostgreSQL)]
public interface IUserModelDefinition
{
    [System.ComponentModel.DataAnnotations.Schema.Column(""user_name"")]
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("[Column(\"user_name\")]", content);
            Assert.DoesNotContain("[Column(\"name\")]", content);
        }

        [Fact]
        public void PostgreSQL_GeneratesJsonbColumn()
        {
            var compilation = RunGenerator(@"
using CSteroid;

namespace System.ComponentModel.DataAnnotations.Schema
{
    public class ColumnAttribute : Attribute
    {
        public ColumnAttribute(string name) { }
        public string TypeName { get; set; }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.PostgreSQL)]
public interface IUserModelDefinition
{
    [PostgreJsonb]
    public string Metadata { get; set; }
    public string FirstName { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("[Column(\"metadata\", TypeName = \"jsonb\")]", content);
            Assert.Contains("[Column(\"first_name\")]", content);
        }

        [Fact]
        public void PostgreSQL_MergesJsonbWithExplicitColumn()
        {
            var compilation = RunGenerator(@"
using CSteroid;

namespace System.ComponentModel.DataAnnotations.Schema
{
    public class ColumnAttribute : Attribute
    {
        public ColumnAttribute(string name) { }
        public string TypeName { get; set; }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.PostgreSQL)]
public interface IUserModelDefinition
{
    [System.ComponentModel.DataAnnotations.Schema.Column(""meta"")]
    [PostgreJsonb]
    public string Metadata { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("[Column(\"meta\", TypeName = \"jsonb\")]", content);
        }
    }
}
