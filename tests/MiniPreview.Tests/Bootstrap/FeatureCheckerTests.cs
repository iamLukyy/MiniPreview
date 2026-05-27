using System;
using MiniPreview.Bootstrap;
using Xunit;

namespace MiniPreview.Tests.Bootstrap;

public class FeatureCheckerTests
{
    [Fact]
    public void RunProbes_ExecutesAllRegisteredProbes()
    {
        var checker = new FeatureChecker();
        checker.AddProbe("A", () => new ProbeResult("A", Ok: true, "ok", null));
        checker.AddProbe("B", () => new ProbeResult("B", Ok: false, "fail", "fix B"));

        var results = checker.RunProbes();

        Assert.Equal(2, results.Length);
        Assert.Equal("A", results[0].Name);
        Assert.True(results[0].Ok);
        Assert.False(results[1].Ok);
        Assert.Equal("fix B", results[1].FixCommand);
    }

    [Fact]
    public void RunProbes_CatchesProbeException_ReportsFailure()
    {
        var checker = new FeatureChecker();
        checker.AddProbe("Boom", () => throw new InvalidOperationException("kaboom"));

        var results = checker.RunProbes();

        Assert.Single(results);
        Assert.False(results[0].Ok);
        Assert.Contains("kaboom", results[0].Detail);
    }

    [Fact]
    public void AllOk_TrueWhenNoFailures()
    {
        var results = new[]
        {
            new ProbeResult("A", true, "ok", null),
            new ProbeResult("B", true, "ok", null)
        };
        Assert.True(FeatureChecker.AllOk(results));
    }

    [Fact]
    public void AllOk_FalseWhenAnyFails()
    {
        var results = new[]
        {
            new ProbeResult("A", true, "ok", null),
            new ProbeResult("B", false, "fail", "fix")
        };
        Assert.False(FeatureChecker.AllOk(results));
    }
}
