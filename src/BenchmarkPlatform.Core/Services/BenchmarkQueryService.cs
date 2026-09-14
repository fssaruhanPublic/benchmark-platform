using BenchmarkPlatform.Core.Data;
using BenchmarkPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BenchmarkPlatform.Core.Services;

/// <summary>
/// Read-side queries backing the dashboard endpoints.
/// </summary>
public class BenchmarkQueryService(BenchmarkDbContext db)
{
    public const double DefaultThreshold = 0.10;

    public async Task<RunListDto> GetRunsAsync(int skip, int take, string? search, CancellationToken ct = default)
    {
        var query = db.Runs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(r =>
                (r.Label != null && EF.Functions.Like(r.Label, pattern)) ||
                EF.Functions.Like(r.MachineName, pattern) ||
                (r.GitCommit != null && EF.Functions.Like(r.GitCommit, pattern)) ||
                (r.GitBranch != null && EF.Functions.Like(r.GitBranch, pattern)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.RunAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .Select(r => new RunSummaryDto(
                r.Id,
                r.RunAt,
                r.Label,
                r.MachineName,
                r.RuntimeVersion,
                r.BuildConfiguration,
                r.GitCommit,
                r.GitBranch,
                r.TotalDurationMs,
                r.Results.Count))
            .ToListAsync(ct);

        return new RunListDto(total, items);
    }

    public async Task<RunDetailDto?> GetRunAsync(Guid id, CancellationToken ct = default)
    {
        var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
        {
            return null;
        }

        var results = await db.Results.AsNoTracking()
            .Where(r => r.RunId == id)
            .OrderBy(r => r.TypeName).ThenBy(r => r.MethodName).ThenBy(r => r.Parameters)
            .ToListAsync(ct);

        return new RunDetailDto(
            ToSummary(run, results.Count),
            results.Select(ToResultDto).ToList());
    }

    public async Task<bool> DeleteRunAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await db.Runs.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    public async Task<IReadOnlyList<BenchmarkIdentityDto>> GetBenchmarksAsync(CancellationToken ct = default)
    {
        var rows = await db.Results.AsNoTracking()
            .Select(r => new
            {
                r.FullName,
                r.TypeName,
                r.MethodName,
                r.Parameters,
                r.MeanNs,
                RunAt = r.Run!.RunAt
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.FullName)
            .Select(g =>
            {
                var latest = g.OrderByDescending(x => x.RunAt).First();
                return new BenchmarkIdentityDto(
                    g.Key,
                    latest.TypeName,
                    latest.MethodName,
                    latest.Parameters,
                    g.Select(x => x.RunAt).Distinct().Count(),
                    latest.RunAt,
                    latest.MeanNs);
            })
            .OrderBy(b => b.TypeName)
            .ThenBy(b => b.MethodName)
            .ThenBy(b => b.Parameters)
            .ToList();
    }

    public async Task<BenchmarkHistoryDto> GetHistoryAsync(string fullName, int limit, CancellationToken ct = default)
    {
        var points = await db.Results.AsNoTracking()
            .Where(r => r.FullName == fullName && r.Success)
            .OrderByDescending(r => r.Run!.RunAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(r => new HistoryPointDto(
                r.RunId,
                r.Run!.RunAt,
                r.Run.Label,
                r.Run.GitCommit,
                r.MeanNs,
                r.StdDevNs,
                r.AllocatedBytes))
            .ToListAsync(ct);

        points.Reverse();
        return new BenchmarkHistoryDto(fullName, points);
    }

    public async Task<ComparisonDto?> CompareAsync(
        Guid baselineRunId,
        Guid targetRunId,
        double threshold = DefaultThreshold,
        CancellationToken ct = default)
    {
        var baselineDetail = await GetRunAsync(baselineRunId, ct);
        var targetDetail = await GetRunAsync(targetRunId, ct);

        if (baselineDetail is null || targetDetail is null)
        {
            return null;
        }

        var rows = BuildComparisonRows(baselineDetail.Results, targetDetail.Results, threshold);

        return new ComparisonDto(
            baselineDetail.Run,
            targetDetail.Run,
            threshold,
            rows.Count(r => r.Status == ComparisonStatus.Regression),
            rows.Count(r => r.Status == ComparisonStatus.Improvement),
            rows);
    }

    /// <summary>
    /// Pure comparison logic: matches cases by FullName and classifies each pair against
    /// <paramref name="threshold"/> (a ratio, e.g. 0.10 = 10%).
    /// </summary>
    public static IReadOnlyList<ComparisonRowDto> BuildComparisonRows(
        IReadOnlyList<BenchmarkResultDto> baseline,
        IReadOnlyList<BenchmarkResultDto> target,
        double threshold)
    {
        var baselineByName = baseline.GroupBy(r => r.FullName).ToDictionary(g => g.Key, g => g.First());
        var targetByName = target.GroupBy(r => r.FullName).ToDictionary(g => g.Key, g => g.First());

        var names = baselineByName.Keys
            .Union(targetByName.Keys)
            .OrderBy(n => n, StringComparer.Ordinal);

        var rows = new List<ComparisonRowDto>();

        foreach (var name in names)
        {
            baselineByName.TryGetValue(name, out var b);
            targetByName.TryGetValue(name, out var t);

            if (b is null)
            {
                rows.Add(new ComparisonRowDto(
                    name, null, t!.MeanNs, null, null, t.AllocatedBytes, null, ComparisonStatus.OnlyInTarget));
                continue;
            }

            if (t is null)
            {
                rows.Add(new ComparisonRowDto(
                    name, b.MeanNs, null, null, b.AllocatedBytes, null, null, ComparisonStatus.OnlyInBaseline));
                continue;
            }

            var meanDelta = Delta(b.MeanNs, t.MeanNs);
            var allocDelta = Delta(b.AllocatedBytes, t.AllocatedBytes);

            var status = meanDelta switch
            {
                null => ComparisonStatus.Neutral,
                var d when d > threshold => ComparisonStatus.Regression,
                var d when d < -threshold => ComparisonStatus.Improvement,
                _ => ComparisonStatus.Neutral
            };

            rows.Add(new ComparisonRowDto(
                name,
                b.MeanNs,
                t.MeanNs,
                meanDelta,
                b.AllocatedBytes,
                t.AllocatedBytes,
                allocDelta,
                status));
        }

        return rows;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var runCount = await db.Runs.CountAsync(ct);
        var resultCount = await db.Results.CountAsync(ct);
        var benchmarkCount = await db.Results.Select(r => r.FullName).Distinct().CountAsync(ct);

        var lastTwo = await db.Runs.AsNoTracking()
            .OrderByDescending(r => r.RunAt)
            .Take(2)
            .Select(r => r.Id)
            .ToListAsync(ct);

        RunSummaryDto? latest = null;
        var regressions = 0;

        if (lastTwo.Count > 0)
        {
            var latestDetail = await GetRunAsync(lastTwo[0], ct);
            latest = latestDetail?.Run;

            if (lastTwo.Count == 2 && latestDetail is not null)
            {
                var previousDetail = await GetRunAsync(lastTwo[1], ct);
                if (previousDetail is not null)
                {
                    regressions = BuildComparisonRows(previousDetail.Results, latestDetail.Results, DefaultThreshold)
                        .Count(r => r.Status == ComparisonStatus.Regression);
                }
            }
        }

        return new DashboardSummaryDto(runCount, benchmarkCount, resultCount, latest, regressions);
    }

    private static double? Delta(double baseline, double target)
        => baseline <= 0 ? null : (target - baseline) / baseline;

    private static RunSummaryDto ToSummary(BenchmarkRun r, int resultCount) => new(
        r.Id,
        r.RunAt,
        r.Label,
        r.MachineName,
        r.RuntimeVersion,
        r.BuildConfiguration,
        r.GitCommit,
        r.GitBranch,
        r.TotalDurationMs,
        resultCount);

    private static BenchmarkResultDto ToResultDto(BenchmarkResult r) => new(
        r.Id,
        r.TypeName,
        r.MethodName,
        r.FullName,
        r.Parameters,
        r.Categories,
        r.IsBaseline,
        r.Success,
        r.MeanNs,
        r.ErrorNs,
        r.StdDevNs,
        r.MedianNs,
        r.MinNs,
        r.MaxNs,
        r.P95Ns,
        r.OpsPerSecond,
        r.Gen0,
        r.Gen1,
        r.Gen2,
        r.AllocatedBytes,
        r.RatioToBaseline);
}
