using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Testing;
using CSteroid.Generator.Generators;
using CSteroid.Generator.Mapping;
using Xunit;

namespace CSteroid.Tests
{
    public class MappingGeneratorTests
    {
        private static readonly IIncrementalGenerator[] Generators = { new CSteroidGenerator(), new CSteroidMappingGenerator() };

        private static Compilation RunGenerator(params string[] sources)
        {
            return GeneratorTestHarness.RunGenerator(Generators, sources);
        }

        [Fact]
        public void ExtensionMethod_GeneratesMapping()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute(""UserDto"", MappingTypes: MappingTypes.ExtensionMethod)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("User.g.cs", files);
            Assert.Contains("UserDto.g.cs", files);
            Assert.Contains("CSteroidMappings.g.cs", files);

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            Assert.Contains("internal static class MappingExtensions", mappings);
            Assert.Contains("MapToUserDto(this User item)", mappings);
            Assert.Contains("MapToUser(this UserDto item)", mappings);
            Assert.Contains("internal partial class UserToUserDtoMapper", mappings);
            // No DI flag: the internal mapper class does not implement IMapper.
            Assert.DoesNotContain("IMapper", mappings);
        }

        [Fact]
        public void NestedAndCollectionModels_AreMappedWithNullSafety()
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

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute(""UserDto"", MappingTypes: MappingTypes.ExtensionMethod)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public Address Address { get; set; }
    public List<Address> Addresses { get; set; }
    public Address[] AddressArray { get; set; }
    public HashSet<Address> AddressSet { get; set; }
}
");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            Assert.Contains("Address = source.Address", mappings);
            Assert.Contains("Addresses = source.Addresses?.ToList()", mappings);
            Assert.Contains("AddressArray = source.AddressArray?.ToArray()", mappings);
            Assert.Contains("AddressSet = source.AddressSet is null ? null : new HashSet<Address>(source.AddressSet)", mappings);
        }

        [Fact]
        public void CircularReference_GeneratesManualMapComment()
        {
            var compilation = RunGenerator(@"
using CSteroid;
using System.Collections.Generic;
using CSteroid.Generated.Models;

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute(""UserDto"", MappingTypes: MappingTypes.ExtensionMethod)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public User Manager { get; set; }
    public List<User> Reports { get; set; }
}
");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            Assert.Contains("/* TODO: circular reference", mappings);
        }

        [Fact]
        public void ExplicitCast_GeneratesOperator()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ExtensionMethod | MappingTypes.ExplicitCast)]
[CanonicalModelAttribute(""UserDto"", MappingTypes: MappingTypes.ExtensionMethod | MappingTypes.ExplicitCast)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("CSteroidCastOperators.g.cs", files);

            var casts = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidCastOperators.g.cs");
            Assert.Contains("public static explicit operator UserDto(User item)", casts);
            Assert.Contains("public static explicit operator User(UserDto item)", casts);
            // The operator-carrying partial declares no marker interfaces.
            Assert.Contains("public partial class User", casts);
            Assert.DoesNotContain("IMappable", casts);
        }

        [Fact]
        public void ProjectionExpression_GeneratesPropertyAndQueryExtension()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ExtensionMethod | MappingTypes.ProjectionExpression)]
[CanonicalModelAttribute(""UserDto"", MappingTypes: MappingTypes.ExtensionMethod | MappingTypes.ProjectionExpression)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            Assert.Contains("public static global::System.Linq.Expressions.Expression<global::System.Func<User, UserDto>> ProjectionExpression", mappings);
            Assert.Contains("source => new UserDto()", mappings);
            Assert.Contains("Id = source.Id", mappings);
            Assert.Contains("Name = source.Name", mappings);
            Assert.Contains("internal static global::System.Linq.IQueryable<UserDto> ProjectToUserDto(this global::System.Linq.IQueryable<User> query)", mappings);
            Assert.Contains("return query.Select(UserToUserDtoMapper.ProjectionExpression);", mappings);
        }

        [Fact]
        public void ProjectionExpression_NestedAndCollectionProperties_AreInlined()
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

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ProjectionExpression)]
[CanonicalModelAttribute(""UserDto"", MappingTypes: MappingTypes.ProjectionExpression)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public Address Address { get; set; }
    public List<Address> Addresses { get; set; }
}
");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            Assert.Contains("Address = (source.Address == null ? null : new Address()", mappings);
            Assert.Contains("Street = source.Address.Street", mappings);
            Assert.Contains("Addresses = (source.Addresses == null ? null : source.Addresses.Select(x => new Address()", mappings);
            Assert.Contains("Street = x.Street", mappings);
            Assert.Contains(").ToList()", mappings);
        }

        [Fact]
        public void CustomNamespace_MappingReferencesCustomNamespace()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", CustomNamespace = ""MyApp.Domain.Models"", MappingTypes = MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute(""UserDto"", MappingTypes = MappingTypes.ExtensionMethod)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            Assert.Contains("using MyApp.Domain.Models;", mappings);
            Assert.Contains("using CSteroid.Generated.Models;", mappings);
            Assert.Contains("internal static class MappingExtensions", mappings);
        }

        [Fact]
        public void CustomNamespace_CastOperatorsGeneratedInCustomNamespace()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", CustomNamespace = ""MyApp.Domain.Models"", MappingTypes = MappingTypes.ExtensionMethod | MappingTypes.ExplicitCast)]
[CanonicalModelAttribute(""UserDto"", CustomNamespace = ""MyApp.Dtos"", MappingTypes = MappingTypes.ExtensionMethod | MappingTypes.ExplicitCast)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var paths = compilation.SyntaxTrees
                .Select(t => t.FilePath)
                .Where(p => !string.IsNullOrEmpty(p) && p.Contains("CSteroidCastOperators.g.cs"))
                .ToArray();

            Assert.Equal(2, paths.Length);
            Assert.Contains(paths, p => p.Contains("MyApp/Domain/Models/CSteroidCastOperators.g.cs") || p.Contains("MyApp\\Domain\\Models\\CSteroidCastOperators.g.cs"));
            Assert.Contains(paths, p => p.Contains("MyApp/Dtos/CSteroidCastOperators.g.cs") || p.Contains("MyApp\\Dtos\\CSteroidCastOperators.g.cs"));

            var castOperatorContent = string.Join("\n", compilation.SyntaxTrees
                .Where(t => t.FilePath != null && t.FilePath.Contains("CSteroidCastOperators.g.cs"))
                .Select(t => t.ToString()));

            Assert.Contains("namespace MyApp.Domain.Models", castOperatorContent);
            Assert.Contains("namespace MyApp.Dtos", castOperatorContent);
            Assert.Contains("using CSteroid.Generated.Mappings;", castOperatorContent);
            Assert.Contains("public static explicit operator UserDto(User item)", castOperatorContent);
            Assert.Contains("public static explicit operator User(UserDto item)", castOperatorContent);
        }

        [Fact]
        public void MappingProject_GeneratesMappingsInConfiguredNamespace()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[assembly: GeneratorConfig(MappingProject = ""MyApp.Infrastructure.Mappings"")]

[CanonicalModelAttribute(""User"", MappingTypes = MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute(""UserDto"", MappingTypes = MappingTypes.ExtensionMethod)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var paths = compilation.SyntaxTrees
                .Select(t => t.FilePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            Assert.Contains(paths, p => p.Contains("MyApp/Infrastructure/Mappings/CSteroidMappings.g.cs") || p.Contains("MyApp\\Infrastructure\\Mappings\\CSteroidMappings.g.cs"));

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            Assert.Contains("namespace MyApp.Infrastructure.Mappings", mappings);
            Assert.Contains("internal static class MappingExtensions", mappings);
        }

        [Fact]
        public void SimpleMapping_GeneratesBranchFreeMapper()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ExtensionMethod, Simple = true)]
[CanonicalModelAttribute(""UserDto"", MappingTypes: MappingTypes.ExtensionMethod, Simple = true)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            // No mapper class: each entry point inlines its own copy of the initializer.
            Assert.DoesNotContain("class UserToUserDtoMapper", mappings);
            Assert.DoesNotContain("class UserDtoToUserMapper", mappings);
            Assert.DoesNotContain("MapCore", mappings);
            Assert.Contains("return new UserDto()", mappings);
            Assert.Contains("return new User()", mappings);
            Assert.Contains("Id = item.Id", mappings);
            Assert.Contains("Name = item.Name", mappings);
            Assert.DoesNotContain("_customMapFunc", mappings);
            Assert.DoesNotContain("ConfigureMapping", mappings);
            Assert.DoesNotContain("MappingHelpers", mappings);
        }

        [Fact]
        public void SimpleMapping_MixedWithRegularModel_OnlySimpleSideIsBranchFree()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ExtensionMethod, Simple = true)]
[CanonicalModelAttribute(""UserDto"", MappingTypes: MappingTypes.ExtensionMethod)]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            // Simple source: no mapper class, initializer inlined into the extension method.
            Assert.DoesNotContain("class UserToUserDtoMapper", mappings);
            Assert.Contains("return new UserDto()", mappings);
            Assert.Contains("Id = item.Id", mappings);
            // Regular source: override hook and helper instance are preserved.
            Assert.Contains("return MappingHelpers._userDtoToUserMapper.Map(item);", mappings);
            Assert.Contains("_customMapFunc", mappings);
        }

        [Fact]
        public void SimpleMapping_WithDependencyInjection_KeepsMapperClass()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ExtensionMethod | MappingTypes.DependencyInjection, Simple = true)]
[CanonicalModelAttribute(""UserDto"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            // DI registers IMapper<Src,Dst> so the mapper class must exist even for Simple,
            // and only then does it implement the interface.
            Assert.Contains("internal sealed partial class UserToUserDtoMapper : global::CSteroid.IMapper<User, UserDto>", mappings);
            Assert.Contains("ServiceCollectionServiceExtensions.AddSingleton<global::CSteroid.IMapper<User, UserDto>, UserToUserDtoMapper>(services);", mappings);
        }

        [Fact]
        public void SimpleMapping_ExplicitCastOperator_InlinesInitializer()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ExplicitCast, Simple = true)]
[CanonicalModelAttribute(""UserDto"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            var casts = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidCastOperators.g.cs");
            Assert.Contains("public static explicit operator UserDto(User item)", casts);
            Assert.Contains("return new UserDto()", casts);
            Assert.Contains("Id = item.Id", casts);
            Assert.DoesNotContain("UserToUserDtoMapper", casts);
            Assert.DoesNotContain("_customMapFunc", casts);
        }

        [Fact]
        public void CastOnly_GeneratesNoMapperClass()
        {
            var compilation = RunGenerator(@"
using CSteroid;

[CanonicalModelAttribute(""User"", MappingTypes: MappingTypes.ImplicitCast)]
[CanonicalModelAttribute(""UserDto"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
}
");

            // A lone cast operator cannot be overridden, so no mapper class is generated
            // for either direction — the initializer is inlined into the operators.
            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.DoesNotContain(files, f => f.Contains("CSteroidMappings"));

            var casts = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidCastOperators.g.cs");
            Assert.Contains("public static implicit operator UserDto(User item)", casts);
            Assert.Contains("return new UserDto()", casts);
            Assert.DoesNotContain("Mapper", casts);
        }

        [Fact]
        public void InterfaceInheritance_BoAndDto_MapInheritedAndOwnProperties()
        {
            // Mapping pairs are declared as two [CanonicalModel] targets on ONE interface;
            // the base interface contributes inherited properties to both models.
            var compilation = RunGenerator(@"
using CSteroid;

public interface IEntityBase
{
    public int Id { get; set; }
    public System.DateTime CreatedAt { get; set; }
}

[CanonicalModelAttribute(""OrderBo"", MappingTypes: MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute(""OrderDto"", MappingTypes: MappingTypes.ExtensionMethod)]
public interface IOrderModelDefinition : IEntityBase
{
    public string Product { get; set; }
    public int Quantity { get; set; }
}
");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            // Inherited base properties participate in the mapping (inside the mapper's
            // DefaultMap initializers, parameter name "source"):
            Assert.Contains("Id = source.Id", mappings);
            Assert.Contains("CreatedAt = source.CreatedAt", mappings);
            // ...alongside the derived interface's own properties:
            Assert.Contains("Product = source.Product", mappings);
            Assert.Contains("Quantity = source.Quantity", mappings);
        }

        [Fact]
        public void InterfaceInheritance_AllPropertiesMapInBothDirections()
        {
            var compilation = RunGenerator(@"
using CSteroid;

public interface IEntityBase
{
    public int Id { get; set; }
}

[CanonicalModelAttribute(""OrderBo"", MappingTypes: MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute(""OrderDto"", MappingTypes: MappingTypes.ExtensionMethod)]
public interface IOrderModelDefinition : IEntityBase
{
    public string Product { get; set; }
    public string Note { get; set; }
}
");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            // Base + derived properties map in both directions (mapper DefaultMap bodies):
            Assert.Contains("Id = source.Id", mappings);
            Assert.Contains("Product = source.Product", mappings);
            Assert.Contains("Note = source.Note", mappings);
        }

        [Fact]
        public void InterfaceInheritance_RuntimeMappingMapsBaseProperties()
        {
            var compilation = RunGenerator(@"
using CSteroid;

public interface IEntityBase
{
    public int Id { get; set; }
}

[CanonicalModelAttribute(""OrderBo"", MappingTypes: MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute(""OrderDto"", MappingTypes: MappingTypes.ExtensionMethod)]
public interface IOrderModelDefinition : IEntityBase
{
    public string Product { get; set; }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var boType = assembly.GetType("CSteroid.Generated.Models.OrderBo");
            var dtoType = assembly.GetType("CSteroid.Generated.Models.OrderDto");
            Assert.NotNull(boType);
            Assert.NotNull(dtoType);

            var bo = System.Activator.CreateInstance(boType!);
            boType!.GetProperty("Id")!.SetValue(bo, 7);
            boType.GetProperty("Product")!.SetValue(bo, "Widget");

            var extensionsType = assembly.GetType("CSteroid.Generated.Mappings.MappingExtensions");
            Assert.NotNull(extensionsType);
            var mapMethod = extensionsType!.GetMethod("MapToOrderDto", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(mapMethod);

            var dto = mapMethod!.Invoke(null, new[] { bo });
            Assert.NotNull(dto);
            Assert.Equal(7, (int)dtoType!.GetProperty("Id")!.GetValue(dto)!);
            Assert.Equal("Widget", (string)dtoType.GetProperty("Product")!.GetValue(dto)!);
        }

        [Fact]
        public void MetadataLib_GeneratesOnlyClaimedNamespace()
        {
            // Simulates: metadata lib holds the interface; a referenced Domain project already
            // generated DomainEntity (hand-written stand-in for its generated output).
            var metadataSource = @"
using CSteroid;

[CanonicalModelAttribute(""DomainEntity"", CustomNamespace = ""Hex.Order.Domain.Models"", MappingTypes = MappingTypes.ExtensionMethod)]
[CanonicalModelAttribute(""OrderDto"", CustomNamespace = ""Hex.Order.Application.Dtos"", MappingTypes = MappingTypes.ExtensionMethod)]
public interface IOrderModelDefinition
{
    public int Id { get; set; }
    public string Product { get; set; }
}
";

            var domainSource = @"
namespace Hex.Order.Domain.Models
{
    public sealed class DomainEntity
    {
        public int Id { get; set; }
        public string Product { get; set; }
    }
}
";

            var consumerSource = @"
using CSteroid;

[assembly: GeneratorConfigAttribute(GenerateOnlyNamespaces = new string[] { ""Hex.Order.Application.Dtos"" })]
";

            var compilation = GeneratorTestHarness.RunGeneratorWithMetadataReference(Generators, new[] { metadataSource, domainSource }, consumerSource);

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("OrderDto.g.cs", files);
            Assert.DoesNotContain(files, f => f == "DomainEntity.g.cs");

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            Assert.Contains("internal static class MappingExtensions", mappings);
            // Source type lives in the referenced metadata assembly's generated Domain project;
            // here it is resolvable via the compilation reference, so the inward mapping is emitted.
            Assert.Contains("MapToOrderDto(this DomainEntity item)", mappings);
            Assert.DoesNotContain("MapToDomainEntity(this OrderDto item)", mappings);
        }

        [Fact]
        public void MetadataLib_DoesNotEmitMappingForUnresolvableSource()
        {
            var metadataSource = @"
using CSteroid;

[CanonicalModelAttribute(""DomainEntity"", CustomNamespace = ""Hex.Order.Domain.Models"")]
[CanonicalModelAttribute(""OrderDto"", CustomNamespace = ""Hex.Order.Application.Dtos"", MappingTypes = MappingTypes.ExtensionMethod)]
public interface IOrderModelDefinition
{
    public int Id { get; set; }
}
";

            // No reference carrying the generated DomainEntity type: the inward rule must skip.
            var consumerSource = @"
using CSteroid;

[assembly: GeneratorConfigAttribute(GenerateOnlyNamespaces = new string[] { ""Hex.Order.Application.Dtos"" })]
";

            var compilation = GeneratorTestHarness.RunGeneratorWithMetadataReference(Generators, new[] { metadataSource }, consumerSource);

            var mappings = GeneratorTestHarness.GetGeneratedSource(compilation, "CSteroidMappings.g.cs");
            Assert.DoesNotContain("MapToOrderDto(this DomainEntity item)", mappings);
        }

        [Fact]
        public void MetadataLib_CastOperatorsOnlyForLocalSource()
        {
            var metadataSource = @"
using CSteroid;

[CanonicalModelAttribute(""OrderBo"", CustomNamespace = ""Hex.Order.Domain.Models"", MappingTypes = MappingTypes.ExplicitCast)]
[CanonicalModelAttribute(""OrderEntity"", CustomNamespace = ""Hex.Order.Domain.Models"", MappingTypes = MappingTypes.ExplicitCast)]
[CanonicalModelAttribute(""OrderDto"", CustomNamespace = ""Hex.Order.Application.Dtos"", MappingTypes = MappingTypes.ExtensionMethod)]
public interface IOrderModelDefinition
{
    public int Id { get; set; }
}
";

            var consumerSource = @"
using CSteroid;

[assembly: GeneratorConfigAttribute(GenerateOnlyNamespaces = new string[] { ""Hex.Order.Application.Dtos"" })]
";

            var compilation = GeneratorTestHarness.RunGeneratorWithMetadataReference(Generators, new[] { metadataSource }, consumerSource);

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.DoesNotContain(files, f => f.Contains("CSteroidCastOperators"));
        }
    }
}
