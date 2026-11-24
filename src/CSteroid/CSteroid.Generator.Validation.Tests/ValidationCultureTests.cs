using System;
using System.Globalization;
using Microsoft.CodeAnalysis;
using CSteroid.Generator.Generators;
using CSteroid.Generator.Testing;
using CSteroid.Generator.Validation;
using Xunit;

namespace CSteroid.Tests
{
    public class ValidationCultureTests
    {
        [Fact]
        public void RangeAttribute_UsesInvariantLiterals_AndValidatesValues()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                CultureInfo.CurrentUICulture = new CultureInfo("de-DE");

                var compilation = GeneratorTestHarness.RunGenerator(
                    new IIncrementalGenerator[] { new CSteroidGenerator(), new CSteroidValidationGenerator() },
                    @"
using System.ComponentModel.DataAnnotations;
using CSteroid;

[CanonicalModel(""Measurement"", ValidationType: ValidationType.DataAnnotations | ValidationType.Custom)]
public interface IMeasurementDefinition
{
    [Range(0.5, 1.5)]
    double Amount { get; set; }
}
");

                var modelSource = GeneratorTestHarness.GetGeneratedSource(compilation, "Measurement.g.cs");
                var validatorSource = GeneratorTestHarness.GetGeneratedSource(compilation, "MeasurementValidator.g.cs");
                Assert.Contains("[Range(0.5d, 1.5d)]", modelSource);
                Assert.Contains(".InclusiveBetween(0.5d, 1.5d)", validatorSource);
                Assert.DoesNotContain("0,5", modelSource + validatorSource);

                var assembly = GeneratorTestHarness.EmitAndLoad(compilation);
                var modelType = assembly.GetType("CSteroid.Generated.Models.Measurement");
                var validatorType = assembly.GetType("CSteroid.Generated.Validation.MeasurementValidator");
                var model = Activator.CreateInstance(modelType!);
                var validator = Activator.CreateInstance(validatorType!);
                var validate = validatorType!.GetMethod("Validate");

                modelType!.GetProperty("Amount")!.SetValue(model, 0.25);
                var invalid = validate!.Invoke(validator, new[] { model });
                Assert.False(GeneratorTestHarness.IsValid(invalid!));

                modelType.GetProperty("Amount")!.SetValue(model, 0.75);
                var valid = validate.Invoke(validator, new[] { model });
                Assert.True(GeneratorTestHarness.IsValid(valid!));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }
    }
}
