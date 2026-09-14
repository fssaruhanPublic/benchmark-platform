namespace BenchmarkPlatform.Core.Services;

public record RunSummaryDto(
    Guid Id,
    DateTimeOffset RunAt,
    string? Label,
    string MachineName,
    string RuntimeVersion,
    string BuildConfiguration,
    string? GitCommit,
    string? GitBranch,
    double TotalDurationMs,
    int ResultCount);

public record BenchmarkResultDto(
    Guid Id,
    string TypeName,
    string MethodName,
    string FullName,
    string Parameters,
    string Categories,
    bool IsBaseline,
    bool Success,
    double MeanNs,
    double ErrorNs,
    double StdDevNs,
    double MedianNs,
    double MinNs,
    double MaxNs,
    double P95Ns,
    double OpsPerSecond,
    double Gen0,
    double Gen1,
    double Gen2,
    long AllocatedBytes,
    double? RatioToBaseline);

public record RunDetailDto(RunSummaryDto Run, IReadOnlyList<BenchmarkResultDto> Results);

public record RunListDto(int Total, IReadOnlyList<RunSummaryDto> Items);

public record BenchmarkIdentityDto(
    string FullName,
    string TypeName,
    string MethodName,
    string Parameters,
    int RunCount,
    DateTimeOffset LastSeenAt,
    double LastMeanNs);

public record HistoryPointDto(
    Guid RunId,
    DateTimeOffset RunAt,
    string? Label,
    string? GitCommit,
    double MeanNs,
    double StdDevNs,
    long AllocatedBytes);

public record BenchmarkHistoryDto(string FullName, IReadOnlyList<HistoryPointDto> Points);

public enum ComparisonStatus
{
    Neutral,
    Regression,
    Improvement,
    OnlyInBaseline,
    OnlyInTarget
}

public record ComparisonRowDto(
    string FullName,
    double? BaselineMeanNs,
    double? TargetMeanNs,
    double? MeanDeltaPercent,
    long? BaselineAllocatedBytes,
    long? TargetAllocatedBytes,
    double? AllocatedDeltaPercent,
    ComparisonStatus Status);

public record ComparisonDto(
    RunSummaryDto Baseline,
    RunSummaryDto Target,
    double Threshold,
    int RegressionCount,
    int ImprovementCount,
    IReadOnlyList<ComparisonRowDto> Rows);

public record DashboardSummaryDto(
    int RunCount,
    int BenchmarkCount,
    int ResultCount,
    RunSummaryDto? LatestRun,
    int RegressionsVsPreviousRun);
