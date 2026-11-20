using Microsoft.CodeAnalysis;

namespace CSteroid.Generator.AutoMediator.Tests;

internal static class AutoMediatorTrackedSteps
{
    private static readonly string[] PlanningSteps =
    [
        "AutoMediator.Compilation.Projection",
        "AutoMediator.Merged.Planning",
        "AutoMediator.Behavior.Planning",
        "AutoMediator.Emission.Planning",
        "AutoMediator.Manifest.Planning",
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
        AssertReason(result, "AutoMediator.Compilation.Projection", IncrementalStepRunReason.Unchanged);
        AssertReason(result, "AutoMediator.Merged.Planning", IncrementalStepRunReason.Cached);
        AssertReason(result, "AutoMediator.Behavior.Planning", IncrementalStepRunReason.Cached);
        AssertReason(result, "AutoMediator.Emission.Planning", IncrementalStepRunReason.Cached);
        AssertReason(result, "AutoMediator.Manifest.Planning", IncrementalStepRunReason.Cached);
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
