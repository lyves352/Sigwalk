using Sigwalk.Core.Services;
using Sigwalk.Core.Services.Scanning;

namespace Sigwalk.Tests;

public class RescanPlannerTests
{
    [Fact]
    public void RemovesDeletedAddsNewAndRescansSurvivors()
    {
        var discovery = new DiscoveryResult();
        discovery.Files.Add(@"C:\root\a.exe");
        discovery.Files.Add(@"C:\root\c.exe");

        var plan = RescanPlanner.Create(
            new[] { @"C:\root" },
            discovery,
            new[] { @"C:\root\a.exe", @"C:\root\b.exe" },
            Array.Empty<string>(),
            _ => true);

        Assert.Equal(new[] { @"C:\root\b.exe" }, plan.RemovedPaths);
        Assert.Equal(new[] { @"C:\root\c.exe" }, plan.AddedPaths);
        Assert.Equal(new[] { @"C:\root\a.exe", @"C:\root\c.exe" }, plan.RescanPaths);
    }

    [Fact]
    public void SelectionOnlyRescansSelectedSurvivors()
    {
        var discovery = new DiscoveryResult();
        discovery.Files.Add(@"C:\root\a.exe");
        discovery.Files.Add(@"C:\root\b.exe");

        var plan = RescanPlanner.Create(
            new[] { @"C:\root" },
            discovery,
            new[] { @"C:\root\a.exe", @"C:\root\b.exe" },
            new[] { @"C:\root\b.exe" },
            _ => true);

        Assert.Empty(plan.RemovedPaths);
        Assert.Empty(plan.AddedPaths);
        Assert.Equal(new[] { @"C:\root\b.exe" }, plan.RescanPaths);
    }

    [Fact]
    public void WithoutRootsMissingFilesAreRemoved()
    {
        var plan = RescanPlanner.Create(
            Array.Empty<string>(),
            new DiscoveryResult(),
            new[] { @"C:\imported\a.exe", @"C:\imported\b.exe" },
            Array.Empty<string>(),
            path => path.EndsWith("a.exe", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(new[] { @"C:\imported\b.exe" }, plan.RemovedPaths);
        Assert.Equal(new[] { @"C:\imported\a.exe" }, plan.RescanPaths);
    }

    [Fact]
    public void RootMatchingIsCaseInsensitive()
    {
        var discovery = new DiscoveryResult();
        discovery.Files.Add(@"C:\Root\a.exe");

        var plan = RescanPlanner.Create(
            new[] { @"c:\root" },
            discovery,
            new[] { @"C:\ROOT\a.exe" },
            Array.Empty<string>(),
            _ => true);

        Assert.Empty(plan.RemovedPaths);
        Assert.Empty(plan.AddedPaths);
        Assert.Single(plan.RescanPaths);
    }

    [Fact]
    public void ExplicitFileRootIsCovered()
    {
        var discovery = new DiscoveryResult();
        var plan = RescanPlanner.Create(
            new[] { @"C:\tools\single.exe" },
            discovery,
            new[] { @"C:\tools\single.exe" },
            Array.Empty<string>(),
            _ => true);

        Assert.Equal(new[] { @"C:\tools\single.exe" }, plan.RemovedPaths);
        Assert.Empty(plan.RescanPaths);
    }
}
