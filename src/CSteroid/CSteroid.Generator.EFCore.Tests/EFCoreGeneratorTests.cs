using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Testing;
using CSteroid.Generator.Generators;
using CSteroid.Generator.EFCore;
using Xunit;

namespace CSteroid.Tests
{
    public class EFCoreGeneratorTests
    {
        private static readonly IIncrementalGenerator[] Generators = { new CSteroidGenerator(), new CSteroidEFCoreGenerator() };

        private static Compilation RunGenerator(params string[] sources)
        {
            return GeneratorTestHarness.RunGenerator(Generators, sources);
        }

        [Fact]
        public void EFCore_GeneratesPrivateParameterlessConstructor()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.EFCore)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("private User() { }", content);
        }

        [Fact]
        public void EFCore_PropagatesKeyAndColumnAttributes()
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
[OrmTarget(OrmTarget.EFCore)]
public interface IUserModelDefinition
{
    [Key]
    public int Id { get; set; }

    [Column(""user_name"")]
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("using System.ComponentModel.DataAnnotations.Schema;", content);
            Assert.Contains("[Key]", content);
            Assert.Contains("[Column(\"user_name\")]", content);
        }

        [Fact]
        public void EFCore_GeneratesIEntityTypeConfigurationStub()
        {
            var compilation = RunGenerator(@"
using CSteroid;

namespace Microsoft.EntityFrameworkCore
{
    public interface IEntityTypeConfiguration<T>
    {
        void Configure(Metadata.Builders.EntityTypeBuilder<T> builder);
    }
}

namespace Microsoft.EntityFrameworkCore.Metadata.Builders
{
    public class EntityTypeBuilder<T>
    {
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.EFCore)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "UserConfiguration.g.cs");
            Assert.Contains("public partial class UserConfiguration", content);
            Assert.Contains(": global::Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<User>", content);
            Assert.Contains("static partial void ConfigureEntity(global::Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<User> builder);", content);
            Assert.Contains("ConfigureEntity(builder);", content);
        }

        [Fact]
        public void EFCore_NoEntityFrameworkReference_SkipsConfigurationStub()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.EFCore)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.DoesNotContain(files, f => f == "UserConfiguration.g.cs");
        }

        [Fact]
        public void EFCore_ForwardPropertyAttributes_ForwardsEfNamespaceAttributes()
        {
            // The real-world scenario: EF Core attributes (Microsoft.EntityFrameworkCore
            // namespace) are outside the DataAnnotations whitelist, so they need the
            // per-target opt-in to land on the generated entity.
            var compilation = RunGenerator(@"
using System;
using CSteroid;

namespace Microsoft.EntityFrameworkCore
{
    public class PrecisionAttribute : Attribute
    {
        public PrecisionAttribute(int precision, int scale) { }
    }
}

[CanonicalModelAttribute(""User"", ForwardPropertyAttributes = true)]
[OrmTarget(OrmTarget.EFCore)]
public interface IUserModelDefinition
{
    [Microsoft.EntityFrameworkCore.Precision(18, 2)]
    public decimal Balance { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("[global::Microsoft.EntityFrameworkCore.Precision(18, 2)]", content);
        }

        [Fact]
        public void EFCore_ForwardPropertyAttributes_NotSet_LeavesEfAttributesOff()
        {
            var compilation = RunGenerator(@"
using System;
using CSteroid;

namespace Microsoft.EntityFrameworkCore
{
    public class PrecisionAttribute : Attribute
    {
        public PrecisionAttribute(int precision, int scale) { }
    }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.EFCore)]
public interface IUserModelDefinition
{
    [Microsoft.EntityFrameworkCore.Precision(18, 2)]
    public decimal Balance { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.DoesNotContain("Precision", content);
        }

        [Fact]
        public void EFCore_NavigationProperties_AreMarkedVirtual()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.Collections.Generic;
using CSteroid.Generated.Models;

[CanonicalModelAttribute(""Address"")]
public interface IAddressModelDefinition
{
    public string Street { get; set; }
}

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.EFCore)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public Address Address { get; set; }
    public List<Address> Addresses { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("public virtual Address Address { get; set; }", content);
            Assert.Contains("public virtual global::System.Collections.Generic.List<Address> Addresses { get; set; }", content);
        }

        [Fact]
        public void EFCore_RecordDeclarationType_GeneratesPrivateConstructor()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", DeclarationType: DeclarationType.Record)]
[OrmTarget(OrmTarget.EFCore)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("public partial record User", content);
            Assert.Contains("private User() { }", content);
        }

        [Fact]
        public void EFCore_StructDeclarationType_DoesNotGeneratePrivateConstructor()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", DeclarationType: DeclarationType.Struct)]
[OrmTarget(OrmTarget.EFCore)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("public partial struct User", content);
            Assert.DoesNotContain("private User() { }", content);
        }
    }
}
