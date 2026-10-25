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
        // Shared Generators / RunGenerator helpers live in ModelGeneratorTests.cs.

        [Fact]
        public void MetadataLib_ReferencedInterface_GeneratesOnlyClaimedNamespace()
        {
            var metadataSource = @"
using CSteroid;

[CanonicalModelAttribute(""OrderBo"", CustomNamespace = ""Hex.Order.Domain.Models"")]
[CanonicalModelAttribute(""OrderDto"", CustomNamespace = ""Hex.Order.Application.Dtos"")]
public interface IOrderModelDefinition
{
    public int Id { get; set; }
    public string Product { get; set; }
}
";

            var consumerSource = @"
using CSteroid;

[assembly: GeneratorConfigAttribute(GenerateOnlyNamespaces = new string[] { ""Hex.Order.Domain.Models"" })]
";

            var compilation = GeneratorTestHarness.RunGeneratorWithMetadataReference(
                new IIncrementalGenerator[] { new CSteroidGenerator() },
                new[] { metadataSource },
                consumerSource);

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("OrderBo.g.cs", files);
            Assert.DoesNotContain(files, f => f == "OrderDto.g.cs");
        }

        [Fact]
        public void MetadataLib_NoFilter_GeneratesEverything()
        {
            var metadataSource = @"
using CSteroid;

[CanonicalModelAttribute(""OrderBo"", CustomNamespace = ""Hex.Order.Domain.Models"")]
[CanonicalModelAttribute(""OrderDto"", CustomNamespace = ""Hex.Order.Application.Dtos"")]
public interface IOrderModelDefinition
{
    public int Id { get; set; }
}
";

            // No [assembly: GeneratorConfig] filter: backward-compatible all-local behavior.
            var consumerSource = "namespace Consumer { }";

            var compilation = GeneratorTestHarness.RunGeneratorWithMetadataReference(
                new IIncrementalGenerator[] { new CSteroidGenerator() },
                new[] { metadataSource },
                consumerSource);

            var files = GeneratorTestHarness.GetGeneratedFileNames(compilation);
            Assert.Contains("OrderBo.g.cs", files);
            Assert.Contains("OrderDto.g.cs", files);
        }

        [Fact]
        public void ConfigDiagnostics_UnclaimedNamespace_Warns()
        {
            var metadataSource = @"
using CSteroid;

[CanonicalModelAttribute(""OrderBo"", CustomNamespace = ""Hex.Order.Domain.Models"")]
public interface IOrderModelDefinition
{
    public int Id { get; set; }
}
";

            var consumerSource = @"
using CSteroid;

[assembly: GeneratorConfigAttribute(GenerateOnlyNamespaces = new string[] { ""Hex.Order.Domain.Models"", ""Hex.Order.Typo.Namespace"" })]
";

            var (_, diagnostics) = GeneratorTestHarness.RunGeneratorWithMetadataReferenceAndDiagnostics(
                new IIncrementalGenerator[] { new CSteroidGenerator() },
                new[] { metadataSource },
                consumerSource);

            Assert.Contains(diagnostics, d => d.Id == "CSG102" && d.GetMessage().Contains("Hex.Order.Typo.Namespace"));
            Assert.DoesNotContain(diagnostics, d => d.Id == "CSG102" && d.GetMessage().Contains("Domain.Models"));
        }

        [Fact]
        public void ConfigDiagnostics_LayeredMetadataWithoutFilter_ReportsInfo()
        {
            var metadataSource = @"
using CSteroid;

[CanonicalModelAttribute(""OrderBo"", CustomNamespace = ""Hex.Order.Domain.Models"")]
[CanonicalModelAttribute(""OrderDto"", CustomNamespace = ""Hex.Order.Application.Dtos"")]
public interface IOrderModelDefinition
{
    public int Id { get; set; }
}
";

            var consumerSource = "namespace Consumer { }";

            var (_, diagnostics) = GeneratorTestHarness.RunGeneratorWithMetadataReferenceAndDiagnostics(
                new IIncrementalGenerator[] { new CSteroidGenerator() },
                new[] { metadataSource },
                consumerSource);

            Assert.Contains(diagnostics, d => d.Id == "CSG103");
        }

        [Fact]
        public void ConfigDiagnostics_SingleNamespaceMetadataWithoutFilter_StaysSilent()
        {
            // Monolith-style metadata lib (one namespace, e.g. a single business domain):
            // generating everything without a filter is deliberate, no nudge.
            var metadataSource = @"
using CSteroid;

[CanonicalModelAttribute(""OrderEntity"", CustomNamespace = ""Hex.Order.Domain.Models"")]
[CanonicalModelAttribute(""OrderBo"", CustomNamespace = ""Hex.Order.Domain.Models"")]
public interface IOrderModelDefinition
{
    public int Id { get; set; }
}
";

            var consumerSource = "namespace Consumer { }";

            var (_, diagnostics) = GeneratorTestHarness.RunGeneratorWithMetadataReferenceAndDiagnostics(
                new IIncrementalGenerator[] { new CSteroidGenerator() },
                new[] { metadataSource },
                consumerSource);

            Assert.DoesNotContain(diagnostics, d => d.Id == "CSG103");
        }

        [Fact]
        public void ConfigDiagnostics_NoReferencedInterfaces_StaysSilent()
        {
            // In-source model definitions only: no metadata lib involved, nothing to nudge about.
            var consumerSource = @"
using CSteroid;

[CanonicalModelAttribute(""User"", CustomNamespace = ""MyApp.Models"")]
[CanonicalModelAttribute(""UserDto"", CustomNamespace = ""MyApp.Dtos"")]
public interface IUserModelDefinition
{
    public int Id { get; set; }
}
";

            var (_, diagnostics) = GeneratorTestHarness.RunGeneratorWithDiagnostics(
                new IIncrementalGenerator[] { new CSteroidGenerator() },
                consumerSource);

            Assert.DoesNotContain(diagnostics, d => d.Id == "CSG103");
            Assert.DoesNotContain(diagnostics, d => d.Id == "CSG102");
        }

        [Fact]
        public void ForwardPropertyAttributes_ForwardsNonWhitelistedAttributes_ToFlaggedTargetOnly()
        {
            // A custom attribute in a consumer namespace (stands in for EF Core's [Precision]):
            // forwarded to the entity target with the flag, absent from the DTO target without it.
            var compilation = RunGenerator(@"
using System;
using CSteroid;

namespace MyApp.Ef
{
    public class PrecisionAttribute : Attribute
    {
        public PrecisionAttribute(int precision, int scale) { }
    }
}

namespace System.ComponentModel.DataAnnotations
{
    public class RequiredAttribute : Attribute { }
}

[CanonicalModelAttribute(""OrderEntity"", ForwardPropertyAttributes = true)]
[CanonicalModelAttribute(""OrderDto"")]
public interface IOrderModelDefinition
{
    [MyApp.Ef.Precision(18, 2)]
    public decimal Total { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public string Product { get; set; }
}
");

            var entity = GeneratorTestHarness.GetGeneratedSource(compilation, "OrderEntity.g.cs");
            Assert.Contains("[global::MyApp.Ef.Precision(18, 2)]", entity);
            // DataAnnotations stay governed by their own pass (ValidationType flag off here),
            // not by the generic forwarder:
            Assert.DoesNotContain("[Required]", entity);

            var dto = GeneratorTestHarness.GetGeneratedSource(compilation, "OrderDto.g.cs");
            Assert.DoesNotContain("Precision", dto);
        }

        [Fact]
        public void ForwardPropertyAttributes_WithoutFlag_ForwardsNothing()
        {
            var compilation = RunGenerator(@"
using System;
using CSteroid;

namespace MyApp.Ef
{
    public class MarkerAttribute : Attribute { }
}

[CanonicalModelAttribute(""OrderEntity"")]
public interface IOrderModelDefinition
{
    [MyApp.Ef.Marker]
    public decimal Total { get; set; }
}
");

            var entity = GeneratorTestHarness.GetGeneratedSource(compilation, "OrderEntity.g.cs");
            Assert.DoesNotContain("[Marker]", entity);
        }

        [Fact]
        public void ForwardPropertyAttributes_NamedArguments_AreForwardedSorted()
        {
            var compilation = RunGenerator(@"
using System;
using CSteroid;

namespace MyApp.Ef
{
    public class CommentAttribute : Attribute
    {
        public CommentAttribute(string text) { Text = text; }
        public string Text { get; }
        public int Order { get; set; }
        public bool IsUnicode { get; set; }
    }
}

[CanonicalModelAttribute(""OrderEntity"", ForwardPropertyAttributes = true)]
public interface IOrderModelDefinition
{
    [MyApp.Ef.Comment(""unit price"", IsUnicode = false, Order = 3)]
    public decimal UnitPrice { get; set; }
}
");

            var entity = GeneratorTestHarness.GetGeneratedSource(compilation, "OrderEntity.g.cs");
            // Named arguments are emitted sorted by name (deterministic output).
            Assert.Contains("[global::MyApp.Ef.Comment(\"unit price\", IsUnicode = false, Order = 3)]", entity);
        }

        [Fact]
        public void ForwardPropertyAttributes_MultipleAttributes_EmittedInDeterministicOrder()
        {
            var compilation = RunGenerator(@"
using System;
using CSteroid;

namespace MyApp.Ef
{
    public class AlphaAttribute : Attribute { }
    public class ZetaAttribute : Attribute { }
}

[CanonicalModelAttribute(""OrderEntity"", ForwardPropertyAttributes = true)]
public interface IOrderModelDefinition
{
    [MyApp.Ef.Zeta]
    [MyApp.Ef.Alpha]
    public decimal Total { get; set; }
}
");

            var entity = GeneratorTestHarness.GetGeneratedSource(compilation, "OrderEntity.g.cs");
            var alphaAt = entity.IndexOf("[global::MyApp.Ef.Alpha", StringComparison.Ordinal);
            var zetaAt = entity.IndexOf("[global::MyApp.Ef.Zeta", StringComparison.Ordinal);
            Assert.True(alphaAt >= 0 && zetaAt >= 0, "both attributes forwarded");
            Assert.True(alphaAt < zetaAt, "attributes sorted by display name for deterministic output");
        }

        [Fact]
        public void ForwardPropertyAttributes_EnumArgument_EmittedFullyQualified()
        {
            var compilation = RunGenerator(@"
using System;
using CSteroid;

namespace MyApp.Ef
{
    public enum StoreAs { Plain, Compressed }

    public class StoreAttribute : Attribute
    {
        public StoreAttribute(StoreAs mode) { }
    }
}

[CanonicalModelAttribute(""OrderEntity"", ForwardPropertyAttributes = true)]
public interface IOrderModelDefinition
{
    [MyApp.Ef.Store(MyApp.Ef.StoreAs.Compressed)]
    public byte[] Blob { get; set; }
}
");

            var entity = GeneratorTestHarness.GetGeneratedSource(compilation, "OrderEntity.g.cs");
            // The generated file has no using for the attribute's namespace, so enum
            // values must be global:: qualified to resolve.
            Assert.True(
                entity.Contains("[global::MyApp.Ef.Store(global::MyApp.Ef.StoreAs.Compressed)]"),
                "Generated entity:\n" + entity);
        }

        [Fact]
        public void ForwardPropertyAttributes_CSteroidAttributes_NotForwarded()
        {
            var compilation = RunGenerator(@"
using System;
using CSteroid;

namespace MyApp.Ef
{
    public class MarkerAttribute : Attribute { }
}

[CanonicalModelAttribute(""OrderEntity"", ForwardPropertyAttributes = true)]
[CanonicalModelAttribute(""OrderDto"")]
public interface IOrderModelDefinition
{
    [MyApp.Ef.Marker]
    [ExcludeFromGeneration(""OrderDto"")]
    public string InternalNotes { get; set; }
}
");

            var entity = GeneratorTestHarness.GetGeneratedSource(compilation, "OrderEntity.g.cs");
            Assert.Contains("[global::MyApp.Ef.Marker]", entity);
            // CSteroid's own attributes are generator input and must not leak onto the model:
            Assert.DoesNotContain("ExcludeFromGeneration", entity);

            var dto = GeneratorTestHarness.GetGeneratedSource(compilation, "OrderDto.g.cs");
            Assert.DoesNotContain("InternalNotes", dto);
        }

        [Fact]
        public void ForwardPropertyAttributes_AttributeAppliedAtRuntime()
        {
            var compilation = RunGenerator(@"
using System;
using CSteroid;

namespace MyApp.Ef
{
    public class PrecisionAttribute : Attribute
    {
        public PrecisionAttribute(int precision, int scale)
        {
            Precision = precision;
            Scale = scale;
        }

        public int Precision { get; }
        public int Scale { get; }
    }
}

[CanonicalModelAttribute(""OrderEntity"", ForwardPropertyAttributes = true)]
public interface IOrderModelDefinition
{
    [MyApp.Ef.Precision(18, 2)]
    public decimal Total { get; set; }
}
");

            var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
            var entityType = assembly.GetType("CSteroid.Generated.Models.OrderEntity");
            Assert.NotNull(entityType);

            var totalProp = entityType!.GetProperty("Total");
            Assert.NotNull(totalProp);

            var attributeData = totalProp!.GetCustomAttributesData()
                .SingleOrDefault(a => a.AttributeType.Name == "PrecisionAttribute");
            Assert.NotNull(attributeData);
            Assert.Equal(18, attributeData!.ConstructorArguments[0].Value);
            Assert.Equal(2, attributeData.ConstructorArguments[1].Value);
        }
    }
}
