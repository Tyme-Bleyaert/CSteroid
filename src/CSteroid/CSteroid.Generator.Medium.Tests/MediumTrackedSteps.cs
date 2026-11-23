using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.Medium.Tests;

internal static class MediumTrackedSteps
{
    private static readonly string[] PlanningSteps =
    [
        "Medium.Compilation.Projection",
        "Medium.Merged.Planning",
        "Medium.Behavior.Planning",
        "Medium.Emission.Planning",
        "Medium.Manifest.Planning",
    ];

    internal static void AssertInitial(GeneratorDriver driver)
    {
        var result = Assert.Single(driver.GetRunResult().Results);
        AssertReason(result, "Compilation", IncrementalStepRunReason.New);
        foreach (var step in PlanningSteps)
            AssertReason(result, step, IncrementalStepRunReason.New);
    }

    internal static void AssertRetained(GeneratorDriver driver)
    {
        var result = Assert.Single(driver.GetRunResult().Results);
        AssertReason(result, "Compilation", IncrementalStepRunReason.Modified);
        AssertReason(result, "Medium.Compilation.Projection", IncrementalStepRunReason.Unchanged);
        AssertReason(result, "Medium.Merged.Planning", IncrementalStepRunReason.Cached);
        AssertReason(result, "Medium.Behavior.Planning", IncrementalStepRunReason.Cached);
        AssertReason(result, "Medium.Emission.Planning", IncrementalStepRunReason.Cached);
        AssertReason(result, "Medium.Manifest.Planning", IncrementalStepRunReason.Cached);
    }

    private static void AssertReason(
        GeneratorRunResult result,
        string trackingName,
        IncrementalStepRunReason expected)
    {
        var steps = Assert.Single(result.TrackedSteps, pair => pair.Key == trackingName).Value;
        var output = Assert.Single(Assert.Single(steps).Outputs);
        Assert.NotNull(output.Value);
        Assert.Equal(expected, output.Reason);
    }
}
