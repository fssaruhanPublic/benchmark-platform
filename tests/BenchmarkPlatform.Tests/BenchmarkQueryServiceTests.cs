using BenchmarkPlatform.Core.Services;

namespace BenchmarkPlatform.Tests;

public class BenchmarkQueryServiceTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2 = new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetRuns_ReturnsNewestFirstWithResultCounts()
    {
        using var temp = new TempDatabase();
        temp.SeedRun(Day1, "first", ("Fast", 100, 0));
        temp.SeedRun(Day2, "second", ("Fast", 120, 0), ("Slow", 900, 64));

        await using var db = temp.CreateContext();
        var list = await new BenchmarkQueryService(db).GetRunsAsync(0, 25, null);

        Assert.Equal(2, list.Total);
        Assert.Equal("second", list.Items[0].Label);
        Assert.Equal(2, list.Items[0].ResultCount);
        Assert.Equal("first", list.Items[1].Label);
    }

    [Fact]
    public async Task GetRuns_FiltersBySearchTerm()
    {
        using var temp = new TempDatabase();
        temp.SeedRun(Day1, "before caching", ("Fast", 100, 0));
        temp.SeedRun(Day2, "after caching", ("Fast", 80, 0));

        await using var db = temp.CreateContext();
        var list = await new BenchmarkQueryService(db).GetRunsAsync(0, 25, "before");

        Assert.Equal(1, list.Total);
        Assert.Equal("before caching", list.Items[0].Label);
    }

    [Fact]
    public async Task GetRun_ReturnsNullForUnknownId()
    {
        using var temp = new TempDatabase();

        await using var db = temp.CreateContext();
        Assert.Null(await new BenchmarkQueryService(db).GetRunAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetHistory_IsOrderedOldestToNewest()
    {
        using var temp = new TempDatabase();
        temp.SeedRun(Day2, "second", ("Fast", 120, 0));
        temp.SeedRun(Day1, "first", ("Fast", 100, 0));

        await using var db = temp.CreateContext();
        var history = await new BenchmarkQueryService(db).GetHistoryAsync("SuiteBenchmarks.Fast", 50);

        Assert.Equal(2, history.Points.Count);
        Assert.Equal(100, history.Points[0].MeanNs);
        Assert.Equal(120, history.Points[1].MeanNs);
    }

    [Fact]
    public async Task DeleteRun_RemovesResultsToo()
    {
        using var temp = new TempDatabase();
        var run = temp.SeedRun(Day1, "first", ("Fast", 100, 0));

        await using (var db = temp.CreateContext())
        {
            Assert.True(await new BenchmarkQueryService(db).DeleteRunAsync(run.Id));
        }

        await using var check = temp.CreateContext();
        Assert.Empty(check.Results);
        Assert.Empty(check.Runs);
    }

    [Fact]
    public async Task Compare_ClassifiesRowsAgainstThreshold()
    {
        using var temp = new TempDatabase();
        var baseline = temp.SeedRun(Day1, "baseline",
            ("Steady", 100, 0), ("Regressed", 100, 0), ("Improved", 100, 0), ("Removed", 100, 0));
        var target = temp.SeedRun(Day2, "target",
            ("Steady", 105, 0), ("Regressed", 130, 0), ("Improved", 70, 0), ("Added", 100, 0));

        await using var db = temp.CreateContext();
        var comparison = await new BenchmarkQueryService(db).CompareAsync(baseline.Id, target.Id, 0.10);

        Assert.NotNull(comparison);
        var rows = comparison!.Rows.ToDictionary(r => r.FullName);

        Assert.Equal(ComparisonStatus.Neutral, rows["SuiteBenchmarks.Steady"].Status);
        Assert.Equal(ComparisonStatus.Regression, rows["SuiteBenchmarks.Regressed"].Status);
        Assert.Equal(ComparisonStatus.Improvement, rows["SuiteBenchmarks.Improved"].Status);
        Assert.Equal(ComparisonStatus.OnlyInBaseline, rows["SuiteBenchmarks.Removed"].Status);
        Assert.Equal(ComparisonStatus.OnlyInTarget, rows["SuiteBenchmarks.Added"].Status);

        Assert.Equal(0.30, rows["SuiteBenchmarks.Regressed"].MeanDeltaPercent!.Value, 6);
        Assert.Equal(1, comparison.RegressionCount);
        Assert.Equal(1, comparison.ImprovementCount);
    }

    [Fact]
    public async Task Compare_TreatsExactThresholdAsNeutral()
    {
        using var temp = new TempDatabase();
        var baseline = temp.SeedRun(Day1, "baseline", ("Edge", 100, 0));
        var target = temp.SeedRun(Day2, "target", ("Edge", 110, 0));

        await using var db = temp.CreateContext();
        var comparison = await new BenchmarkQueryService(db).CompareAsync(baseline.Id, target.Id, 0.10);

        Assert.Equal(ComparisonStatus.Neutral, comparison!.Rows.Single().Status);
    }

    [Fact]
    public async Task Compare_ReturnsNullWhenARunIsMissing()
    {
        using var temp = new TempDatabase();
        var baseline = temp.SeedRun(Day1, "baseline", ("Fast", 100, 0));

        await using var db = temp.CreateContext();
        Assert.Null(await new BenchmarkQueryService(db).CompareAsync(baseline.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Summary_CountsRegressionsAgainstThePreviousRun()
    {
        using var temp = new TempDatabase();
        temp.SeedRun(Day1, "baseline", ("Fast", 100, 0), ("Slow", 100, 0));
        temp.SeedRun(Day2, "latest", ("Fast", 300, 0), ("Slow", 100, 0));

        await using var db = temp.CreateContext();
        var summary = await new BenchmarkQueryService(db).GetSummaryAsync();

        Assert.Equal(2, summary.RunCount);
        Assert.Equal(2, summary.BenchmarkCount);
        Assert.Equal(4, summary.ResultCount);
        Assert.Equal("latest", summary.LatestRun!.Label);
        Assert.Equal(1, summary.RegressionsVsPreviousRun);
    }

    [Fact]
    public async Task GetBenchmarks_GroupsCasesAcrossRuns()
    {
        using var temp = new TempDatabase();
        temp.SeedRun(Day1, "first", ("Fast", 100, 0));
        temp.SeedRun(Day2, "second", ("Fast", 120, 0), ("Slow", 900, 0));

        await using var db = temp.CreateContext();
        var benchmarks = await new BenchmarkQueryService(db).GetBenchmarksAsync();

        Assert.Equal(2, benchmarks.Count);
        var fast = benchmarks.Single(b => b.MethodName == "Fast");
        Assert.Equal(2, fast.RunCount);
        Assert.Equal(120, fast.LastMeanNs);
    }
}
