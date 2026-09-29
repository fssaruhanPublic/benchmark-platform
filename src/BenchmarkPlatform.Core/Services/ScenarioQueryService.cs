using System.Text.Json;
using BenchmarkPlatform.Core.Data;
using BenchmarkPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BenchmarkPlatform.Core.Services;

/// <summary>
/// Read-side queries for scenario runs (eval sets, load tests). Mirrors
/// <see cref="BenchmarkQueryService"/>: list, detail, per-metric history and a
/// run-to-run comparison that flags changes beyond a relative threshold.
/// </summary>
public class ScenarioQueryService(BenchmarkDbContext db)
{
    public async Task<ScenarioRunListDto> GetRunsAsync(string? kind, string? suite, int skip, int take,
        CancellationToken ct = default)
    {
        var query = db.ScenarioRuns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(kind)) query = query.Where(r => r.Kind == kind);
        if (!string.IsNullOrWhiteSpace(suite)) query = query.Where(r => r.Suite == suite);

        var total = await query.CountAsync(ct);
        var runs = await query
            .OrderByDescending(r => r.StartedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

        var metrics = await RunMetricsAsync(runs.Select(r => r.Id).ToList(), ct);
        return new ScenarioRunListDto(total, runs.Select(r => ToSummary(r, metrics)).ToList());
    }

    public async Task<ScenarioRunDetailDto?> GetRunAsync(Guid id, CancellationToken ct = default)
    {
        var run = await db.ScenarioRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) return null;

        var cases = await db.ScenarioCases.AsNoTracking()
            .Where(c => c.RunId == id)
            .OrderBy(c => c.Group).ThenBy(c => c.Name)
            .ToListAsync(ct);

        var allMetrics = await db.ScenarioMetrics.AsNoTracking()
            .Where(m => m.RunId == id)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

        var runMetrics = new Dictionary<Guid, List<ScenarioMetric>>
        {
            [id] = allMetrics.Where(m => m.CaseId is null).ToList()
        };
        var byCase = allMetrics.Where(m => m.CaseId is not null).ToLookup(m => m.CaseId!.Value);

        var environment = JsonSerializer.Deserialize<Dictionary<string, string>>(run.EnvironmentJson)
                          ?? new Dictionary<string, string>();

        return new ScenarioRunDetailDto(
            ToSummary(run, runMetrics),
            environment,
            cases.Select(c => new ScenarioCaseDto(
                c.Id, c.Name, c.Group, c.Success, c.Message,
                c.DetailsJson is null ? null : JsonSerializer.Deserialize<JsonElement>(c.DetailsJson),
                byCase[c.Id].Select(ToMetricDto).ToList())).ToList());
    }

    public async Task<bool> DeleteRunAsync(Guid id, CancellationToken ct = default) =>
        await db.ScenarioRuns.Where(r => r.Id == id).ExecuteDeleteAsync(ct) > 0;

    public async Task<IReadOnlyList<ScenarioSuiteDto>> GetSuitesAsync(CancellationToken ct = default)
    {
        var runs = await db.ScenarioRuns.AsNoTracking()
            .Select(r => new { r.Id, r.Suite, r.Kind, r.StartedAt })
            .ToListAsync(ct);

        // Metric and case names are small, distinct sets; pulling them per suite keeps the
        // SQL trivial and SQLite-friendly.
        var metricNames = await db.ScenarioMetrics.AsNoTracking()
            .Select(m => new { m.RunId, m.Name, IsRun = m.CaseId == null })
            .Distinct()
            .ToListAsync(ct);
        var caseNames = await db.ScenarioCases.AsNoTracking()
            .Select(c => new { c.RunId, c.Name })
            .ToListAsync(ct);

        var suiteOfRun = runs.ToDictionary(r => r.Id, r => r.Suite);

        return runs
            .GroupBy(r => r.Suite)
            .Select(g =>
            {
                var latest = g.MaxBy(r => r.StartedAt)!;
                var names = metricNames.Where(m => suiteOfRun.GetValueOrDefault(m.RunId) == g.Key).ToList();
                return new ScenarioSuiteDto(
                    g.Key,
                    latest.Kind,
                    g.Count(),
                    latest.StartedAt,
                    names.Where(n => n.IsRun).Select(n => n.Name).Distinct().Order().ToList(),
                    names.Where(n => !n.IsRun).Select(n => n.Name).Distinct().Order().ToList(),
                    caseNames.Where(c => suiteOfRun.GetValueOrDefault(c.RunId) == g.Key)
                        .Select(c => c.Name).Distinct().Order().ToList());
            })
            .OrderByDescending(s => s.LastRunAt)
            .ToList();
    }

    /// <summary>
    /// A metric over the suite's runs, oldest first. With <paramref name="caseName"/> null the
    /// run-level metric is returned; otherwise the named case's metric.
    /// </summary>
    public async Task<ScenarioHistoryDto> GetHistoryAsync(string suite, string metric, string? caseName, int limit,
        CancellationToken ct = default)
    {
        var runs = await db.ScenarioRuns.AsNoTracking()
            .Where(r => r.Suite == suite)
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(r => new { r.Id, r.StartedAt, r.Label, r.GitCommit })
            .ToListAsync(ct);
        var runIds = runs.Select(r => r.Id).ToList();

        Dictionary<Guid, (Guid? CaseId, bool Success)> caseByRun = [];
        if (caseName is not null)
        {
            caseByRun = await db.ScenarioCases.AsNoTracking()
                .Where(c => runIds.Contains(c.RunId) && c.Name == caseName)
                .ToDictionaryAsync(c => c.RunId, c => ((Guid?)c.Id, c.Success), ct);
        }

        var metrics = await db.ScenarioMetrics.AsNoTracking()
            .Where(m => runIds.Contains(m.RunId) && m.Name == metric)
            .ToListAsync(ct);

        var points = new List<ScenarioHistoryPointDto>();
        string unit = string.Empty;
        var direction = MetricDirection.None;

        foreach (var run in runs.OrderBy(r => r.StartedAt))
        {
            ScenarioMetric? value;
            bool? success = null;

            if (caseName is null)
            {
                value = metrics.FirstOrDefault(m => m.RunId == run.Id && m.CaseId == null);
            }
            else
            {
                if (!caseByRun.TryGetValue(run.Id, out var found)) continue;
                success = found.Success;
                value = metrics.FirstOrDefault(m => m.RunId == run.Id && m.CaseId == found.CaseId);
            }

            if (value is null) continue;
            unit = value.Unit;
            direction = value.Direction;
            points.Add(new ScenarioHistoryPointDto(run.Id, run.StartedAt, run.Label, run.GitCommit, value.Value, success));
        }

        return new ScenarioHistoryDto(suite, metric, caseName, unit,
            ScenarioRunImporter.FormatDirection(direction), points);
    }

    /// <summary>
    /// Compares every metric that has a direction. Metrics without a direction are listed
    /// as <see cref="ComparisonStatus.Neutral"/>: the dashboard cannot know whether a change
    /// in, say, "rows returned" is good or bad.
    /// </summary>
    public async Task<ScenarioComparisonDto?> CompareAsync(Guid baselineId, Guid targetId, double threshold,
        CancellationToken ct = default)
    {
        var baseline = await GetRunAsync(baselineId, ct);
        var target = await GetRunAsync(targetId, ct);
        if (baseline is null || target is null) return null;

        threshold = threshold > 0 ? threshold : BenchmarkQueryService.DefaultThreshold;
        var rows = new List<ScenarioComparisonRowDto>();

        rows.AddRange(CompareMetrics(null, baseline.Run.Metrics, target.Run.Metrics, threshold));

        var baselineCases = baseline.Cases.ToDictionary(c => c.Name, StringComparer.Ordinal);
        var targetCases = target.Cases.ToDictionary(c => c.Name, StringComparer.Ordinal);
        var caseNames = baselineCases.Keys.Union(targetCases.Keys).Order(StringComparer.Ordinal).ToList();
        var outcomeChanges = new List<ScenarioCaseOutcomeChangeDto>();

        foreach (var name in caseNames)
        {
            baselineCases.TryGetValue(name, out var left);
            targetCases.TryGetValue(name, out var right);

            rows.AddRange(CompareMetrics(name, left?.Metrics ?? [], right?.Metrics ?? [], threshold));

            if (left?.Success != right?.Success)
                outcomeChanges.Add(new ScenarioCaseOutcomeChangeDto(name, left?.Success, right?.Success));
        }

        return new ScenarioComparisonDto(
            baseline.Run,
            target.Run,
            threshold,
            rows.Count(r => r.Status == ComparisonStatus.Regression)
                + outcomeChanges.Count(c => c.BaselineSuccess == true && c.TargetSuccess == false),
            rows.Count(r => r.Status == ComparisonStatus.Improvement)
                + outcomeChanges.Count(c => c.BaselineSuccess == false && c.TargetSuccess == true),
            rows,
            outcomeChanges);
    }

    private static IEnumerable<ScenarioComparisonRowDto> CompareMetrics(string? caseName,
        IReadOnlyList<ScenarioMetricDto> baseline, IReadOnlyList<ScenarioMetricDto> target, double threshold)
    {
        var left = baseline.ToDictionary(m => m.Name, StringComparer.Ordinal);
        var right = target.ToDictionary(m => m.Name, StringComparer.Ordinal);

        foreach (var name in left.Keys.Union(right.Keys).Order(StringComparer.Ordinal))
        {
            left.TryGetValue(name, out var b);
            right.TryGetValue(name, out var t);
            var reference = t ?? b!;

            double? delta = b is not null && t is not null && b.Value != 0
                ? (t.Value - b.Value) / Math.Abs(b.Value)
                : null;

            var status = (b, t) switch
            {
                (null, _) => ComparisonStatus.OnlyInTarget,
                (_, null) => ComparisonStatus.OnlyInBaseline,
                _ => Classify(reference.Direction, b.Value, t.Value, delta, threshold)
            };

            yield return new ScenarioComparisonRowDto(caseName, name, reference.Unit, reference.Direction,
                b?.Value, t?.Value, delta, status);
        }
    }

    private static ComparisonStatus Classify(string direction, double baseline, double target, double? delta,
        double threshold)
    {
        if (direction == "none") return ComparisonStatus.Neutral;

        // A zero baseline has no relative delta; any movement off zero counts as a change.
        var changed = delta is { } d ? Math.Abs(d) >= threshold : baseline != target;
        if (!changed) return ComparisonStatus.Neutral;

        var increased = target > baseline;
        var better = direction == "higher" ? increased : !increased;
        return better ? ComparisonStatus.Improvement : ComparisonStatus.Regression;
    }

    private async Task<Dictionary<Guid, List<ScenarioMetric>>> RunMetricsAsync(List<Guid> runIds, CancellationToken ct)
    {
        var metrics = await db.ScenarioMetrics.AsNoTracking()
            .Where(m => runIds.Contains(m.RunId) && m.CaseId == null)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

        return metrics.GroupBy(m => m.RunId).ToDictionary(g => g.Key, g => g.ToList());
    }

    private static ScenarioRunSummaryDto ToSummary(ScenarioRun run, IReadOnlyDictionary<Guid, List<ScenarioMetric>> metrics) =>
        new(run.Id, run.Kind, run.Suite, run.Label, run.StartedAt, run.FinishedAt,
            (run.FinishedAt - run.StartedAt).TotalMilliseconds, run.GitCommit, run.GitBranch, run.MachineName,
            run.Target, run.CaseCount, run.PassedCount,
            metrics.TryGetValue(run.Id, out var list) ? list.Select(ToMetricDto).ToList() : [],
            // Ortam listede de donuyor: panel "yutulan SQL hatasi" gibi notlari
            // her kosunun detayini ayri cekmeden gosterebilsin. Kucuk bir sozluk.
            ParseEnvironment(run.EnvironmentJson));

    private static IReadOnlyDictionary<string, string> ParseEnvironment(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? []; }
        catch (JsonException) { return new Dictionary<string, string>(); }
    }

    private static ScenarioMetricDto ToMetricDto(ScenarioMetric metric) =>
        new(metric.Name, metric.Value, metric.Unit, ScenarioRunImporter.FormatDirection(metric.Direction));
}
