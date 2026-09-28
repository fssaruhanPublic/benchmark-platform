using System.Text.Json;
using BenchmarkPlatform.Core.Data;
using BenchmarkPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BenchmarkPlatform.Core.Services;

/// <summary>Metadata the caller supplies alongside a BenchmarkDotNet JSON report.</summary>
public record BdnImportMetadata(Guid? RunId, string? Label, string? GitCommit, string? GitBranch,
    string? MachineName = null);

/// <summary>
/// Imports BenchmarkDotNet's own "full" JSON report (<c>JsonExporter.Full</c>) into the
/// same model the in-process exporter writes. This lets a project that has no reference to
/// this platform publish micro-benchmarks: it only needs BenchmarkDotNet's standard output.
///
/// BenchmarkDotNet writes one report per benchmark class; posting several reports with the
/// same <see cref="BdnImportMetadata.RunId"/> appends them to one run, mirroring
/// <see cref="BenchmarkRunStore.Save"/>.
/// </summary>
public class BdnJsonImporter(BenchmarkDbContext db)
{
    public async Task<(Guid RunId, int Imported)> ImportAsync(string json, BdnImportMetadata metadata,
        CancellationToken ct = default)
    {
        var run = Map(json, metadata);
        var existing = await db.Runs.FirstOrDefaultAsync(r => r.Id == run.Id, ct);

        if (existing is null)
        {
            db.Runs.Add(run);
        }
        else
        {
            foreach (var result in run.Results)
            {
                result.RunId = existing.Id;
                db.Results.Add(result);
            }

            existing.RunAt = run.RunAt;
            existing.TotalDurationMs += run.TotalDurationMs;
        }

        await db.SaveChangesAsync(ct);
        return (run.Id, run.Results.Count);
    }

    /// <summary>Pure mapping; throws <see cref="ScenarioValidationException"/> on a malformed report.</summary>
    public static BenchmarkRun Map(string json, BdnImportMetadata metadata)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ScenarioValidationException([$"Not valid JSON: {exception.Message}"]);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("Benchmarks", out var benchmarks) ||
                benchmarks.ValueKind != JsonValueKind.Array)
            {
                throw new ScenarioValidationException(
                    ["Expected a BenchmarkDotNet full JSON report with a 'Benchmarks' array (JsonExporter.Full)."]);
            }

            var host = root.TryGetProperty("HostEnvironmentInfo", out var h) ? h : default;

            var run = new BenchmarkRun
            {
                Id = metadata.RunId ?? Guid.NewGuid(),
                RunAt = DateTimeOffset.UtcNow,
                Label = Trim(metadata.Label, 200),
                MachineName = Trim(metadata.MachineName, 200) ?? "imported",
                OsDescription = Text(host, "OsVersion") ?? string.Empty,
                RuntimeVersion = Text(host, "RuntimeVersion") ?? string.Empty,
                ProcessorName = Text(host, "ProcessorName") ?? string.Empty,
                ProcessorCount = Int(host, "LogicalCoreCount") ?? 0,
                Architecture = Text(host, "Architecture") ?? string.Empty,
                BuildConfiguration = Text(host, "Configuration") ?? string.Empty,
                GitCommit = Trim(metadata.GitCommit, 60),
                GitBranch = Trim(metadata.GitBranch, 200),
            };

            foreach (var benchmark in benchmarks.EnumerateArray())
                run.Results.Add(MapResult(benchmark, run.Id));

            ApplyBaselineRatios(run.Results);
            return run;
        }
    }

    private static BenchmarkResult MapResult(JsonElement benchmark, Guid runId)
    {
        var type = Text(benchmark, "Type") ?? "Unknown";
        var method = Text(benchmark, "Method") ?? "Unknown";
        var parameters = Text(benchmark, "Parameters") ?? string.Empty;

        var stats = benchmark.TryGetProperty("Statistics", out var s) && s.ValueKind == JsonValueKind.Object ? s : default;
        var memory = benchmark.TryGetProperty("Memory", out var m) && m.ValueKind == JsonValueKind.Object ? m : default;
        var percentiles = stats.ValueKind == JsonValueKind.Object && stats.TryGetProperty("Percentiles", out var p) ? p : default;

        var mean = Double(stats, "Mean");
        var operations = Double(memory, "TotalOperations") ?? 0;
        double PerThousand(string name) => operations > 0 ? (Double(memory, name) ?? 0) / operations * 1000 : 0;

        return new BenchmarkResult
        {
            RunId = runId,
            Namespace = Text(benchmark, "Namespace") ?? string.Empty,
            TypeName = type,
            MethodName = method,
            // Same identity the in-process exporter builds, so trends line up across both paths.
            FullName = string.IsNullOrWhiteSpace(parameters) ? $"{type}.{method}" : $"{type}.{method}({parameters})",
            Parameters = parameters,
            Categories = string.Empty,
            IsBaseline = (Text(benchmark, "DisplayInfo") ?? string.Empty).Contains("[Baseline]", StringComparison.Ordinal)
                         || benchmark.TryGetProperty("Baseline", out var baseline) && baseline.ValueKind == JsonValueKind.True,
            Success = mean is > 0,
            MeanNs = mean ?? 0,
            ErrorNs = Double(stats, "StandardError") ?? 0,
            StdDevNs = Double(stats, "StandardDeviation") ?? 0,
            MedianNs = Double(stats, "Median") ?? 0,
            MinNs = Double(stats, "Min") ?? 0,
            MaxNs = Double(stats, "Max") ?? 0,
            P95Ns = Double(percentiles, "P95") ?? 0,
            OpsPerSecond = mean is > 0 ? 1_000_000_000d / mean.Value : 0,
            Gen0 = PerThousand("Gen0Collections"),
            Gen1 = PerThousand("Gen1Collections"),
            Gen2 = PerThousand("Gen2Collections"),
            AllocatedBytes = (long)(Double(memory, "BytesAllocatedPerOperation") ?? 0)
        };
    }

    private static void ApplyBaselineRatios(IEnumerable<BenchmarkResult> results)
    {
        foreach (var group in results.GroupBy(r => (r.TypeName, r.Parameters)))
        {
            var baseline = group.FirstOrDefault(r => r.IsBaseline && r.MeanNs > 0);
            if (baseline is null) continue;
            foreach (var result in group)
                result.RatioToBaseline = result.MeanNs > 0 ? result.MeanNs / baseline.MeanNs : null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Int(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    private static double? Double(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value.Trim() : value.Trim()[..max];
}
