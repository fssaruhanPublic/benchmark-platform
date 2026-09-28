namespace BenchmarkPlatform.Core.Entities;

/// <summary>
/// One execution of a scenario suite that is not a BenchmarkDotNet micro-benchmark:
/// an evaluation set ("eval"), a load test ("load"), or anything else that produces
/// named metrics per case. Stored separately from <see cref="BenchmarkRun"/> because the
/// shape is open-ended — metrics are name/value rows rather than fixed columns.
/// </summary>
public class ScenarioRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>What kind of run this is, e.g. <c>eval</c> or <c>load</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The suite name; trends and comparisons are only meaningful within a suite.</summary>
    public string Suite { get; set; } = string.Empty;

    public string? Label { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public string? GitCommit { get; set; }

    public string? GitBranch { get; set; }

    public string MachineName { get; set; } = string.Empty;

    /// <summary>The system under test, e.g. a base URL.</summary>
    public string? Target { get; set; }

    /// <summary>Free-form environment details as a JSON object of strings.</summary>
    public string EnvironmentJson { get; set; } = "{}";

    public int CaseCount { get; set; }

    public int PassedCount { get; set; }

    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ScenarioCase> Cases { get; set; } = [];
}

public class ScenarioCase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }
    public ScenarioRun? Run { get; set; }

    /// <summary>Stable identity used for trends and comparisons.</summary>
    public string Name { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public bool Success { get; set; }

    /// <summary>Short human explanation, typically why the case failed.</summary>
    public string? Message { get; set; }

    /// <summary>Arbitrary JSON for drill-down (expected vs actual, raw responses…).</summary>
    public string? DetailsJson { get; set; }

    public List<ScenarioMetric> Metrics { get; set; } = [];
}

/// <summary>Whether a larger value is better — drives regression/improvement in comparisons.</summary>
public enum MetricDirection
{
    None = 0,
    HigherIsBetter = 1,
    LowerIsBetter = 2
}

public class ScenarioMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RunId { get; set; }

    /// <summary>Null for run-level metrics.</summary>
    public Guid? CaseId { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Value { get; set; }

    /// <summary>Free text: <c>ms</c>, <c>ratio</c>, <c>tokens</c>, <c>USD</c>, <c>req/s</c>…</summary>
    public string Unit { get; set; } = string.Empty;

    public MetricDirection Direction { get; set; }
}
