namespace BenchmarkPlatform.Core.Entities;

/// <summary>
/// One execution of the benchmark suite (or a filtered subset of it).
/// </summary>
public class BenchmarkRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>When the run finished.</summary>
    public DateTimeOffset RunAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional human label, passed via <c>--label</c>.</summary>
    public string? Label { get; set; }

    public string MachineName { get; set; } = string.Empty;
    public string OsDescription { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
    public string ProcessorName { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public string BuildConfiguration { get; set; } = string.Empty;

    public string? GitCommit { get; set; }
    public string? GitBranch { get; set; }

    public double TotalDurationMs { get; set; }

    public List<BenchmarkResult> Results { get; set; } = [];
}
