using BenchmarkPlatform.Reporting;

namespace BenchmarkPlatform.Tests;

public class BenchmarkResultMapperTests
{
    private static RawCaseMeasurement Measurement(
        string type = "MyBenchmarks",
        string method = "Fast",
        string parameters = "",
        bool isBaseline = false,
        double? mean = 1_000d,
        bool success = true) => new(
            Namespace: "Sample",
            TypeName: type,
            MethodName: method,
            Parameters: parameters,
            Categories: "core",
            IsBaseline: isBaseline,
            Success: success,
            MeanNs: mean,
            StandardErrorNs: 5,
            StdDevNs: 20,
            MedianNs: mean,
            MinNs: mean,
            MaxNs: mean,
            P95Ns: mean,
            Gen0: 1.5,
            Gen1: 0,
            Gen2: 0,
            AllocatedBytes: 512);

    [Fact]
    public void FullName_OmitsEmptyParameters()
    {
        var result = BenchmarkResultMapper.ToResult(Measurement());

        Assert.Equal("MyBenchmarks.Fast", result.FullName);
    }

    [Fact]
    public void FullName_IncludesParameters()
    {
        var result = BenchmarkResultMapper.ToResult(Measurement(parameters: "Count=10"));

        Assert.Equal("MyBenchmarks.Fast(Count=10)", result.FullName);
    }

    [Fact]
    public void OpsPerSecond_IsDerivedFromMean()
    {
        var result = BenchmarkResultMapper.ToResult(Measurement(mean: 1_000_000d));

        Assert.Equal(1_000d, result.OpsPerSecond, 6);
    }

    [Fact]
    public void MissingStatistics_MarkTheCaseAsFailed()
    {
        var result = BenchmarkResultMapper.ToResult(Measurement(mean: null));

        Assert.False(result.Success);
        Assert.Equal(0d, result.MeanNs);
        Assert.Equal(0d, result.OpsPerSecond);
    }

    [Fact]
    public void FailedCase_StaysFailedEvenWithStatistics()
    {
        var result = BenchmarkResultMapper.ToResult(Measurement(success: false));

        Assert.False(result.Success);
    }

    [Fact]
    public void ApplyBaselineRatios_ComparesWithinTypeAndParameterSet()
    {
        var results = new[]
        {
            BenchmarkResultMapper.ToResult(Measurement(method: "Slow", parameters: "N=1", isBaseline: true, mean: 200)),
            BenchmarkResultMapper.ToResult(Measurement(method: "Fast", parameters: "N=1", mean: 50)),
            BenchmarkResultMapper.ToResult(Measurement(method: "Slow", parameters: "N=2", isBaseline: true, mean: 400)),
            BenchmarkResultMapper.ToResult(Measurement(method: "Fast", parameters: "N=2", mean: 100))
        };

        BenchmarkResultMapper.ApplyBaselineRatios(results);

        Assert.Equal(1d, results[0].RatioToBaseline);
        Assert.Equal(0.25d, results[1].RatioToBaseline);
        Assert.Equal(1d, results[2].RatioToBaseline);
        Assert.Equal(0.25d, results[3].RatioToBaseline);
    }

    [Fact]
    public void ApplyBaselineRatios_LeavesRatioNullWithoutBaseline()
    {
        var results = new[] { BenchmarkResultMapper.ToResult(Measurement()) };

        BenchmarkResultMapper.ApplyBaselineRatios(results);

        Assert.Null(results[0].RatioToBaseline);
    }
}
