using System.Text.Json;

namespace BenchmarkPlatform.Core.Services;

// ---------------------------------------------------------------------------
// Ingest contract: "scenario-run/v1".
//
// Any tool (an eval runner, a load tester, a script) can POST this document to
// /api/scenario-runs. The dashboard does not reference the producer and the
// producer does not reference the dashboard — the JSON is the only coupling.
// ---------------------------------------------------------------------------

public record ScenarioRunDocument
{
    public string? Schema { get; init; }
    public string? Kind { get; init; }
    public string? Suite { get; init; }
    public string? Label { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public string? GitCommit { get; init; }
    public string? GitBranch { get; init; }
    public string? MachineName { get; init; }
    public string? Target { get; init; }
    public Dictionary<string, string>? Environment { get; init; }
    public List<ScenarioMetricDocument>? Metrics { get; init; }
    public List<ScenarioCaseDocument>? Cases { get; init; }
}

public record ScenarioCaseDocument
{
    public string? Name { get; init; }
    public string? Group { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }
    public List<ScenarioMetricDocument>? Metrics { get; init; }
    public JsonElement? Details { get; init; }
}

public record ScenarioMetricDocument
{
    public string? Name { get; init; }
    public double Value { get; init; }
    public string? Unit { get; init; }

    /// <summary><c>higher</c>, <c>lower</c> or <c>none</c> (default).</summary>
    public string? Direction { get; init; }
}

// ---------------------------------------------------------------------------
// Read-side DTOs.
// ---------------------------------------------------------------------------

public record ScenarioMetricDto(string Name, double Value, string Unit, string Direction);

public record ScenarioRunSummaryDto(
    Guid Id,
    string Kind,
    string Suite,
    string? Label,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    double DurationMs,
    string? GitCommit,
    string? GitBranch,
    string MachineName,
    string? Target,
    int CaseCount,
    int PassedCount,
    IReadOnlyList<ScenarioMetricDto> Metrics,
    IReadOnlyDictionary<string, string> Environment);

public record ScenarioCaseDto(
    Guid Id,
    string Name,
    string Group,
    bool Success,
    string? Message,
    JsonElement? Details,
    IReadOnlyList<ScenarioMetricDto> Metrics);

public record ScenarioRunDetailDto(
    ScenarioRunSummaryDto Run,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<ScenarioCaseDto> Cases);

public record ScenarioRunListDto(int Total, IReadOnlyList<ScenarioRunSummaryDto> Items);

public record ScenarioSuiteDto(
    string Suite,
    string Kind,
    int RunCount,
    DateTimeOffset LastRunAt,
    IReadOnlyList<string> RunMetrics,
    IReadOnlyList<string> CaseMetrics,
    IReadOnlyList<string> Cases);

public record ScenarioHistoryPointDto(
    Guid RunId,
    DateTimeOffset StartedAt,
    string? Label,
    string? GitCommit,
    double Value,
    bool? Success);

public record ScenarioHistoryDto(
    string Suite,
    string Metric,
    string? Case,
    string Unit,
    string Direction,
    IReadOnlyList<ScenarioHistoryPointDto> Points);

public record ScenarioComparisonRowDto(
    string? Case,
    string Metric,
    string Unit,
    string Direction,
    double? BaselineValue,
    double? TargetValue,
    double? DeltaPercent,
    ComparisonStatus Status);

public record ScenarioCaseOutcomeChangeDto(string Case, bool? BaselineSuccess, bool? TargetSuccess);

public record ScenarioComparisonDto(
    ScenarioRunSummaryDto Baseline,
    ScenarioRunSummaryDto Target,
    double Threshold,
    int RegressionCount,
    int ImprovementCount,
    IReadOnlyList<ScenarioComparisonRowDto> Rows,
    IReadOnlyList<ScenarioCaseOutcomeChangeDto> OutcomeChanges);

public record ScenarioImportResultDto(Guid Id, int CaseCount, int MetricCount);
