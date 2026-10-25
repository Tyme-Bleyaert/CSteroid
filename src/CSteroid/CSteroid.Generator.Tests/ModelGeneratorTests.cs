using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Testing;
using CSteroid.Generator.Generators;
using Xunit;

namespace CSteroid.Tests
{
    public partial class ModelGeneratorTests
    {
        private static readonly IIncrementalGenerator[] Generators = { new CSteroidGenerator() };

        private static Compilation RunGenerator(params string[] sources)
        {
            return GeneratorTestHarness.RunGenerator(Generators, sources);
        }
        [Fact]
        public void SimpleModel_GeneratesPartialClass()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("User.g.cs", files);

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("public partial class User", content);
            Assert.Contains("public int Id { get; set; }", content);
            Assert.Contains("public string Name { get; set; }", content);
        }

        [Fact]
        public void SimpleMapping_GeneratesSealedModel()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", Simple = true)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("public sealed class User", content);
            Assert.DoesNotContain("partial", content);
        }

        [Fact]
        public void SimpleMapping_WithCastFlags_StaysPartial()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", Simple = true, MappingTypes = MappingTypes.ImplicitCast)]
[CanonicalModelAttribute(""UserDto"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            // The mapping generator merges its cast-operator part into the class,
            // so it must remain partial even though it is sealed.
            Assert.Contains("public sealed partial class User", content);
        }

        [Fact]
        public void SimpleMapping_DeclaredTwice_StaysPartial()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", Simple = true)]
public interface IFirstDefinition
{
    public int Id { get; set; }
}

[CanonicalModelAttribute(""User"", Simple = true)]
public interface ISecondDefinition
{
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            // 'partial' is what merges duplicate target declarations.
            Assert.Contains("public sealed partial class User", content);
        }

        [Fact]
        public void SimpleMapping_WithStructDeclaration_StaysUnsealed()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", Simple = true, DeclarationType = DeclarationType.Struct)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            // 'sealed' is invalid on structs; they are implicitly sealed already.
            Assert.Contains("public struct User", content);
            Assert.DoesNotContain("sealed", content);
            Assert.DoesNotContain("partial", content);
        }

        [Fact]
        public void ExcludedPropertiesAttribute_RemovesProperty()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", ExcludedProperties: new[] { ""Name"" })]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("public int Id { get; set; }", content);
            Assert.DoesNotContain("public string Name { get; set; }", content);
        }

        [Fact]
        public void ExcludeFromGenerationAttribute_RemovesPropertyForSpecificModel()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"")]
[CanonicalModelAttribute(""UserDto"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    [ExcludeFromGeneration(""UserDto"")]
    public string Name { get; set; }
}
");

            var user = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("public string Name { get; set; }", user);

            var userDto = GeneratorTestHarness.GetGeneratedSource(compilation, "UserDto.g.cs");
            Assert.DoesNotContain("public string Name { get; set; }", userDto);
        }

        [Fact]
        public void BaseInterfaceProperties_AreIncluded()
        {
            var compilation = RunGenerator(@"
using CSteroid;

public interface IBaseModel
{
    public int Id { get; set; }
}

[CanonicalModelAttribute(""User"")]
public interface IUserModelDefinition : IBaseModel
{
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("public int Id { get; set; }", content);
            Assert.Contains("public string Name { get; set; }", content);
        }

        [Fact]
        public void DataAnnotations_ForwardedToModel()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.ComponentModel.DataAnnotations;

namespace System.ComponentModel.DataAnnotations
{
    public class MaxLengthAttribute : Attribute
    {
        public MaxLengthAttribute(int length) { }
    }
}

[CanonicalModelAttribute(""User"", ValidationType: ValidationType.DataAnnotations)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("[MaxLength(50)]", content);
            Assert.Contains("public string Name { get; set; }", content);
        }

        [Fact]
        public void XmlDocumentation_IsForwardedToGeneratedModel()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"")]
public interface IUserModelDefinition
{
    /// <summary>Gets or sets the identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    /// <remarks>Used for display purposes.</remarks>
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("/// <summary>Gets or sets the identifier.</summary>", content);
            Assert.Contains("/// <summary>Gets or sets the name.</summary>", content);
            Assert.Contains("/// <remarks>Used for display purposes.</remarks>", content);
        }

        [Fact]
        public void XmlDocumentation_InheritedFromBaseInterface()
        {
            var compilation = RunGenerator(@"
using CSteroid;

public interface IBaseModel
{
    /// <summary>The primary key.</summary>
    public int Id { get; set; }
}

[CanonicalModelAttribute(""User"")]
public interface IUserModelDefinition : IBaseModel
{
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("/// <summary>The primary key.</summary>", content);
        }

        [Fact]
        public void RecordDeclarationType_GeneratesRecord()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", DeclarationType: DeclarationType.Record)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("public partial record User", content);
        }

        [Fact]
        public void NoCanonicalAttribute_GeneratesNothing()
        {
            var compilation = RunGenerator(@"
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Empty(files);
        }

        [Fact]
        public void FileNamingConvention_Default_UsesDotG()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[assembly: GeneratorConfig(FileNamingConvention = FileNamingConvention.Default)]

[CanonicalModelAttribute(""User"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("User.g.cs", files);
            Assert.DoesNotContain("User.model.cs", files);
        }

        [Fact]
        public void CustomNamespace_GeneratesModelInCustomNamespace()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", CustomNamespace = ""MyApp.Domain.Models"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var paths = compilation.SyntaxTrees
                .Select(t => t.FilePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            Assert.Contains(paths, p => p.Contains("MyApp/Domain/Models/User.g.cs") || p.Contains("MyApp\\Domain\\Models\\User.g.cs"));

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("namespace MyApp.Domain.Models", content);
            Assert.Contains("public partial class User", content);
        }

        [Fact]
        public void CustomNamespace_DerivesFolderPathFromNamespace()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", CustomNamespace = ""MyApp.Domain.Models"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var paths = compilation.SyntaxTrees
                .Select(t => t.FilePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            Assert.Contains(paths, p => p.Contains("MyApp/Domain/Models/User.g.cs") || p.Contains("MyApp\\Domain\\Models\\User.g.cs"));
        }

        [Fact]
        public void FolderPath_OverridesNamespaceDerivedFolder()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", CustomNamespace = ""MyApp.Domain.Models"", FolderPath = ""Entities/Users"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var paths = compilation.SyntaxTrees
                .Select(t => t.FilePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            Assert.Contains(paths, p => p.Contains("Entities/Users/User.g.cs") || p.Contains("Entities\\Users\\User.g.cs"));
            Assert.DoesNotContain(paths, p => p.Contains("MyApp/Domain/Models/User.g.cs") || p.Contains("MyApp\\Domain\\Models\\User.g.cs"));

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.Contains("namespace MyApp.Domain.Models", content);
        }

        [Fact]
        public void FileNamingConvention_ModelSuffix_UsesDotModel()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[assembly: GeneratorConfig(FileNamingConvention = FileNamingConvention.ModelSuffix)]

[CanonicalModelAttribute(""User"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("User.model.cs", files);
            Assert.DoesNotContain("User.g.cs", files);
        }

        [Fact]
        public void ConflictingOrmFlags_ReportsDiagnosticError()
        {
            var (_, diagnostics) = GeneratorTestHarness.RunGeneratorWithDiagnostics(Generators, @"
using CSteroid;

[CanonicalModelAttribute(""User"")]
[OrmTarget(OrmTarget.EFCore | OrmTarget.MongoDB)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
");

            var csg101 = diagnostics.Where(d => d.Id == "CSG101").ToList();
            Assert.Single(csg101);
            Assert.Contains("EFCore, MongoDB", csg101[0].GetMessage());
        }

        [Fact]
        public void InterfaceInheritance_DerivedModelIncludesBaseProperties()
        {
            var compilation = RunGenerator(@"
using CSteroid;

public interface IEntityBase
{
    public int Id { get; set; }
    public System.DateTime CreatedAt { get; set; }
}

[CanonicalModelAttribute(""Order"")]
public interface IOrderModelDefinition : IEntityBase
{
    public string Product { get; set; }
    public int Quantity { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "Order.g.cs");
            Assert.Contains("public partial class Order", content);
            // Inherited from IEntityBase:
            Assert.Contains("public int Id { get; set; }", content);
            Assert.Contains("public global::System.DateTime CreatedAt { get; set; }", content);
            // Declared on the derived interface:
            Assert.Contains("public string Product { get; set; }", content);
            Assert.Contains("public int Quantity { get; set; }", content);
        }

        [Fact]
        public void InterfaceInheritance_MultiLevel_GathersAllBaseProperties()
        {
            var compilation = RunGenerator(@"
using CSteroid;

public interface IEntityBase
{
    public int Id { get; set; }
}

public interface IAuditedEntity : IEntityBase
{
    public string CreatedBy { get; set; }
}

[CanonicalModelAttribute(""Product"")]
public interface IProductModelDefinition : IAuditedEntity
{
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "Product.g.cs");
            Assert.Contains("public int Id { get; set; }", content);        // IEntityBase
            Assert.Contains("public string CreatedBy { get; set; }", content); // IAuditedEntity
            Assert.Contains("public string Name { get; set; }", content);      // derived
        }

        [Fact]
        public void InterfaceInheritance_DerivedRedeclaredProperty_ShadowsBase()
        {
            var compilation = RunGenerator(@"
using CSteroid;

public interface IEntityBase
{
    public string Id { get; set; }
}

[CanonicalModelAttribute(""Customer"")]
public interface ICustomerModelDefinition : IEntityBase
{
    public new int Id { get; set; }
    public string Name { get; set; }
}
");

            var content = GeneratorTestHarness.GetGeneratedSource(compilation, "Customer.g.cs");
            // The derived declaration wins; the shadowed base property is not duplicated.
            Assert.Contains("public int Id { get; set; }", content);
            Assert.DoesNotContain("public string Id { get; set; }", content);
            Assert.Contains("public string Name { get; set; }", content);
        }

        [Fact]
        public void InterfaceInheritance_BaseInterfaceWithoutAttribute_IsNotGenerated()
        {
            var compilation = RunGenerator(@"
using CSteroid;

public interface IEntityBase
{
    public int Id { get; set; }
}

[CanonicalModelAttribute(""Order"")]
public interface IOrderModelDefinition : IEntityBase
{
    public string Product { get; set; }
}
");

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("Order.g.cs", files);
            // IEntityBase carries no [CanonicalModel] so no model is emitted for it.
            Assert.DoesNotContain(files, f => f.Contains("EntityBase"));
        }
    }
}
