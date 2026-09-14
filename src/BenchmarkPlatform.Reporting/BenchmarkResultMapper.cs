using BenchmarkPlatform.Core.Entities;

namespace BenchmarkPlatform.Reporting;

/// <summary>
/// Turns raw measurements into persistable entities. Pure — no BenchmarkDotNet, no database.
/// </summary>
public static class BenchmarkResultMapper
{
    private const double NanosecondsPerSecond = 1_000_000_000d;

    public static BenchmarkResult ToResult(RawCaseMeasurement measurement)
    {
        var mean = measurement.MeanNs ?? 0d;

        return new BenchmarkResult
        {
            Namespace = measurement.Namespace,
            TypeName = measurement.TypeName,
            MethodName = measurement.MethodName,
            FullName = BuildFullName(measurement.TypeName, measurement.MethodName, measurement.Parameters),
            Parameters = measurement.Parameters,
            Categories = measurement.Categories,
            IsBaseline = measurement.IsBaseline,
            Success = measurement.Success && measurement.MeanNs.HasValue,
            MeanNs = mean,
            ErrorNs = measurement.StandardErrorNs ?? 0d,
            StdDevNs = measurement.StdDevNs ?? 0d,
            MedianNs = measurement.MedianNs ?? 0d,
            MinNs = measurement.MinNs ?? 0d,
            MaxNs = measurement.MaxNs ?? 0d,
            P95Ns = measurement.P95Ns ?? 0d,
            OpsPerSecond = mean > 0 ? NanosecondsPerSecond / mean : 0d,
            Gen0 = measurement.Gen0,
            Gen1 = measurement.Gen1,
            Gen2 = measurement.Gen2,
            AllocatedBytes = measurement.AllocatedBytes
        };
    }

    public static string BuildFullName(string typeName, string methodName, string parameters)
        => string.IsNullOrWhiteSpace(parameters)
            ? $"{typeName}.{methodName}"
            : $"{typeName}.{methodName}({parameters})";

    /// <summary>
    /// Fills <see cref="BenchmarkResult.RatioToBaseline"/> for every case whose declaring type
    /// has a <c>[Benchmark(Baseline = true)]</c> method. Cases are compared within the same
    /// parameter set, which is how BenchmarkDotNet groups them too.
    /// </summary>
    public static void ApplyBaselineRatios(IEnumerable<BenchmarkResult> results)
    {
        foreach (var group in results.GroupBy(r => (r.TypeName, r.Parameters)))
        {
            var baseline = group.FirstOrDefault(r => r.IsBaseline && r.MeanNs > 0);
            if (baseline is null)
            {
                continue;
            }

            foreach (var result in group)
            {
                result.RatioToBaseline = result.MeanNs > 0 ? result.MeanNs / baseline.MeanNs : null;
            }
        }
    }
}
