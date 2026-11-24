using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CSteroid.Generator.Generators;
using CSteroid.Generator.Shared;
using CSteroid.Generator.Testing;
using Xunit;

namespace CSteroid.Tests
{
    public class IncrementalGenerationTests
    {
        [Fact]
        public void NonCanonicalInterface_DoesNotWarn_WhenWarningsAreErrors()
        {
            var compilation = GeneratorTestHarness.CreateCompilation(
                new[]
                {
                    CSharpSyntaxTree.ParseText(@"
using System;

[Obsolete]
public interface IExternalContract
{
    string Name { get; set; }
}
")
                },
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    generalDiagnosticOption: ReportDiagnostic.Error));

            CSharpGeneratorDriver.Create(new CSteroidGenerator())
                .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CSG001");
            Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void UnrelatedEdit_ReusesPlan_AndModelEditsInvalidateIt()
        {
            var userTree = CSharpSyntaxTree.ParseText(@"
using System.ComponentModel.DataAnnotations;
using CSteroid;

[CanonicalModel(""User"", ValidationType: ValidationType.DataAnnotations)]
[CanonicalModel(""UserDto"")]
public interface IUserDefinition
{
    int Id { get; set; }

    [MaxLength(10)]
    string Name { get; set; }
}
", path: "user.cs");
            var orderTree = CSharpSyntaxTree.ParseText(@"
using CSteroid;

[CanonicalModel(""Order"")]
public interface IOrderDefinition
{
    int Id { get; set; }
}
", path: "order.cs");
            var unrelatedTree = CSharpSyntaxTree.ParseText(
                "public sealed class Unrelated { public int Value { get; set; } }",
                path: "unrelated.cs");
            var compilation = GeneratorTestHarness.CreateCompilation(new[] { userTree, orderTree, unrelatedTree });
            var driver = Run(CreateDriver(), compilation, out _);
            var originalOutput = Snapshots(driver);
            Assert.Equal(3, originalOutput.Length);
            AssertPlanning(driver, IncrementalStepRunReason.New);
            AssertStep(driver, InterfaceDiscovery.CompilationProjectionTrackingName, IncrementalStepRunReason.New);

            var editedUnrelatedTree = CSharpSyntaxTree.ParseText(
                "public sealed class Unrelated { public int Value { get; set; } public string Extra { get; set; } }",
                path: unrelatedTree.FilePath);
            var unrelatedCompilation = compilation.ReplaceSyntaxTree(unrelatedTree, editedUnrelatedTree);
            driver = Run(driver, unrelatedCompilation, out _);
            Assert.Equal(originalOutput, Snapshots(driver));
            AssertPlanning(driver, IncrementalStepRunReason.Cached);
            AssertStep(driver, InterfaceDiscovery.CompilationProjectionTrackingName, IncrementalStepRunReason.Unchanged);

            var nullableTree = CSharpSyntaxTree.ParseText(
                userTree.ToString().Replace("string Name", "string? Name"),
                path: userTree.FilePath);
            var nullableCompilation = unrelatedCompilation.ReplaceSyntaxTree(userTree, nullableTree);
            driver = Run(driver, nullableCompilation, out _);
            var nullableOutput = Snapshots(driver);
            Assert.NotEqual(originalOutput, nullableOutput);
            Assert.Contains(nullableOutput, text => text.Contains("string? Name", StringComparison.Ordinal));
            AssertPlanning(driver, IncrementalStepRunReason.Modified);

            var attributeTree = CSharpSyntaxTree.ParseText(
                nullableTree.ToString().Replace("MaxLength(10)", "MaxLength(20)"),
                path: userTree.FilePath);
            var attributeCompilation = nullableCompilation.ReplaceSyntaxTree(nullableTree, attributeTree);
            driver = Run(driver, attributeCompilation, out _);
            var attributeOutput = Snapshots(driver);
            Assert.NotEqual(nullableOutput, attributeOutput);
            Assert.Contains(attributeOutput, text => text.Contains("[MaxLength(20)]", StringComparison.Ordinal));
            AssertPlanning(driver, IncrementalStepRunReason.Modified);
        }

        [Fact]
        public void NullableProperty_PreservesAnnotationInGeneratedModel()
        {
            var compilation = GeneratorTestHarness.RunGenerator(
                new IIncrementalGenerator[] { new CSteroidGenerator() },
                @"
#nullable enable
using CSteroid;

[CanonicalModel(""User"")]
public interface IUserDefinition
{
    string? Name { get; set; }
}
");

            var source = GeneratorTestHarness.GetGeneratedSource(compilation, "User.g.cs");
            Assert.StartsWith("// <auto-generated />\n#nullable enable", source);
            Assert.Contains("public string? Name { get; set; }", source);
        }

        private static GeneratorDriver CreateDriver()
        {
            return CSharpGeneratorDriver.Create(
                new[] { new CSteroidGenerator().AsSourceGenerator() },
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));
        }

        private static GeneratorDriver Run(GeneratorDriver driver, Compilation compilation, out Compilation output)
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out var diagnostics);
            Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            return driver;
        }

        private static string[] Snapshots(GeneratorDriver driver)
        {
            return Assert.Single(driver.GetRunResult().Results).GeneratedSources
                .OrderBy(source => source.HintName, StringComparer.Ordinal)
                .Select(source => source.HintName + "\n" + source.SourceText)
                .ToArray();
        }

        private static void AssertPlanning(GeneratorDriver driver, IncrementalStepRunReason reason) =>
            AssertStep(driver, InterfaceDiscovery.GenerationPlanningTrackingName, reason);

        private static void AssertStep(GeneratorDriver driver, string trackingName, IncrementalStepRunReason reason)
        {
            var result = Assert.Single(driver.GetRunResult().Results);
            var steps = Assert.Single(result.TrackedSteps, pair => pair.Key == trackingName).Value;
            var output = Assert.Single(Assert.Single(steps).Outputs);
            Assert.Equal(reason, output.Reason);
        }
    }
}
