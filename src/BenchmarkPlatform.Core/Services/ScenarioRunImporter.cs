using System.Text.Json;
using BenchmarkPlatform.Core.Data;
using BenchmarkPlatform.Core.Entities;

namespace BenchmarkPlatform.Core.Services;

/// <summary>A document that does not satisfy the <c>scenario-run/v1</c> contract.</summary>
public class ScenarioValidationException(IReadOnlyList<string> errors)
    : Exception("Invalid scenario run: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>
/// Validates a <see cref="ScenarioRunDocument"/> and stores it. Validation is strict on
/// purpose: these runs are used as evidence, and a silently coerced value (a NaN latency,
/// an unnamed case) would make a comparison lie without anyone noticing.
/// </summary>
public class ScenarioRunImporter(BenchmarkDbContext db)
{
    public const string SchemaV1 = "scenario-run/v1";

    public const int MaxCases = 10_000;
    public const int MaxMetricsPerOwner = 200;
    public const int MaxDetailsLength = 64 * 1024;

    public async Task<ScenarioImportResultDto> ImportAsync(ScenarioRunDocument document, CancellationToken ct = default)
    {
        var (run, runMetrics) = Map(document);

        db.ScenarioRuns.Add(run);
        db.ScenarioMetrics.AddRange(runMetrics);
        await db.SaveChangesAsync(ct);

        return new ScenarioImportResultDto(run.Id, run.Cases.Count,
            runMetrics.Count + run.Cases.Sum(c => c.Metrics.Count));
    }

    /// <summary>Validates and maps without touching the database.</summary>
    public static (ScenarioRun Run, List<ScenarioMetric> RunMetrics) Map(ScenarioRunDocument document)
    {
        var errors = new List<string>();

        if (document.Schema != SchemaV1)
            errors.Add($"schema must be '{SchemaV1}'.");

        Required(document.Kind, "kind", 40, errors);
        Required(document.Suite, "suite", 200, errors);
        Optional(document.Label, "label", 200, errors);
        Optional(document.GitCommit, "gitCommit", 60, errors);
        Optional(document.GitBranch, "gitBranch", 200, errors);
        Optional(document.MachineName, "machineName", 200, errors);
        Optional(document.Target, "target", 500, errors);

        if (document.StartedAt is null) errors.Add("startedAt is required.");
        if (document.StartedAt is { } s && document.FinishedAt is { } f && f < s)
            errors.Add("finishedAt must not be earlier than startedAt.");

        var cases = document.Cases ?? [];
        if (cases.Count > MaxCases) errors.Add($"at most {MaxCases} cases are accepted.");

        var runId = Guid.NewGuid();
        var runMetrics = MapMetrics(document.Metrics, runId, null, "metrics", errors);

        var seenCases = new HashSet<string>(StringComparer.Ordinal);
        var mappedCases = new List<ScenarioCase>(cases.Count);

        for (var i = 0; i < cases.Count; i++)
        {
            var source = cases[i];
            var path = $"cases[{i}]";

            if (Required(source.Name, $"{path}.name", 400, errors) && !seenCases.Add(source.Name!))
                errors.Add($"{path}.name '{source.Name}' is duplicated; case names identify trends.");
            Optional(source.Group, $"{path}.group", 200, errors);

            string? details = null;
            if (source.Details is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } element)
            {
                details = element.GetRawText();
                if (details.Length > MaxDetailsLength)
                    errors.Add($"{path}.details exceeds {MaxDetailsLength} characters.");
            }

            var caseId = Guid.NewGuid();
            mappedCases.Add(new ScenarioCase
            {
                Id = caseId,
                RunId = runId,
                Name = source.Name?.Trim() ?? string.Empty,
                Group = source.Group?.Trim() ?? string.Empty,
                Success = source.Success,
                Message = Truncate(source.Message, 2000),
                DetailsJson = details,
                Metrics = MapMetrics(source.Metrics, runId, caseId, $"{path}.metrics", errors)
            });
        }

        if (errors.Count > 0) throw new ScenarioValidationException(errors);

        var started = document.StartedAt!.Value.ToUniversalTime();
        var run = new ScenarioRun
        {
            Id = runId,
            Kind = document.Kind!.Trim(),
            Suite = document.Suite!.Trim(),
            Label = Blank(document.Label),
            StartedAt = started,
            FinishedAt = (document.FinishedAt ?? started).ToUniversalTime(),
            GitCommit = Blank(document.GitCommit),
            GitBranch = Blank(document.GitBranch),
            MachineName = document.MachineName?.Trim() ?? string.Empty,
            Target = Blank(document.Target),
            EnvironmentJson = JsonSerializer.Serialize(document.Environment ?? new Dictionary<string, string>()),
            CaseCount = mappedCases.Count,
            PassedCount = mappedCases.Count(c => c.Success),
            Cases = mappedCases
        };

        // Run-level metrics hang off the run by RunId only (no navigation property), so
        // they are returned separately and added to the context explicitly.
        return (run, runMetrics);
    }

    private static List<ScenarioMetric> MapMetrics(List<ScenarioMetricDocument>? metrics, Guid runId, Guid? caseId,
        string path, List<string> errors)
    {
        var result = new List<ScenarioMetric>();
        if (metrics is null) return result;

        if (metrics.Count > MaxMetricsPerOwner)
        {
            errors.Add($"{path}: at most {MaxMetricsPerOwner} metrics are accepted.");
            return result;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < metrics.Count; i++)
        {
            var metric = metrics[i];
            var itemPath = $"{path}[{i}]";

            if (Required(metric.Name, $"{itemPath}.name", 120, errors) && !seen.Add(metric.Name!))
                errors.Add($"{itemPath}.name '{metric.Name}' is duplicated.");
            Optional(metric.Unit, $"{itemPath}.unit", 40, errors);

            if (!double.IsFinite(metric.Value))
                errors.Add($"{itemPath}.value must be a finite number.");

            var direction = ParseDirection(metric.Direction);
            if (direction is null)
                errors.Add($"{itemPath}.direction must be 'higher', 'lower' or 'none'.");

            result.Add(new ScenarioMetric
            {
                RunId = runId,
                CaseId = caseId,
                Name = metric.Name?.Trim() ?? string.Empty,
                Value = metric.Value,
                Unit = metric.Unit?.Trim() ?? string.Empty,
                Direction = direction ?? MetricDirection.None
            });
        }

        return result;
    }

    public static MetricDirection? ParseDirection(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "none" => MetricDirection.None,
        "higher" or "higher-is-better" => MetricDirection.HigherIsBetter,
        "lower" or "lower-is-better" => MetricDirection.LowerIsBetter,
        _ => null
    };

    public static string FormatDirection(MetricDirection direction) => direction switch
    {
        MetricDirection.HigherIsBetter => "higher",
        MetricDirection.LowerIsBetter => "lower",
        _ => "none"
    };

    private static bool Required(string? value, string name, int max, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} is required.");
            return false;
        }

        return Optional(value, name, max, errors);
    }

    private static bool Optional(string? value, string name, int max, List<string> errors)
    {
        if (value is not null && value.Trim().Length > max)
        {
            errors.Add($"{name} must be at most {max} characters.");
            return false;
        }

        return true;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
