namespace BenchmarkPlatform.Reporting;

/// <summary>
/// BenchmarkDotNet-free view of a single measured case. Keeping this in between the
/// <c>Summary</c> object and the entities makes the mapping unit-testable.
/// Timings are nanoseconds; null means BenchmarkDotNet produced no statistics.
/// </summary>
public sealed record RawCaseMeasurement(
    string Namespace,
    string TypeName,
    string MethodName,
    string Parameters,
    string Categories,
    bool IsBaseline,
    bool Success,
    double? MeanNs,
    double? StandardErrorNs,
    double? StdDevNs,
    double? MedianNs,
    double? MinNs,
    double? MaxNs,
    double? P95Ns,
    double Gen0,
    double Gen1,
    double Gen2,
    long AllocatedBytes);
