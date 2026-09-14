using System.Runtime.InteropServices;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkPlatform.Core.Data;
using BenchmarkPlatform.Core.Entities;
using BenchmarkPlatform.Core.Services;

namespace BenchmarkPlatform.Reporting;

/// <summary>
/// BenchmarkDotNet exporter that writes the summary straight into the dashboard database,
/// so a finished run shows up in the UI without any extra step.
/// </summary>
public class SqliteDashboardExporter(string? connectionString = null) : IExporter
{
    public string Name => nameof(SqliteDashboardExporter);

    public void ExportToLog(Summary summary, ILogger logger)
    {
        // Nothing to write to the console log; the database write happens in ExportToFiles.
    }

    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        try
        {
            var run = BuildRun(summary);
            var count = BenchmarkRunStore.Save(run, connectionString);

            consoleLogger.WriteLine(LogKind.Statistic,
                $"[Dashboard] Kayit tamam - bu kosuda toplam {count} sonuc. " +
                $"Run Id: {run.Id}, DB: {BenchmarkDbPath.Resolve()}");
        }
        catch (Exception ex)
        {
            // A reporting failure must never fail the benchmark run itself.
            consoleLogger.WriteLine(LogKind.Error, $"[Dashboard] Sonuclar kaydedilemedi: {ex.Message}");
        }

        return [];
    }

    internal static BenchmarkRun BuildRun(Summary summary)
    {
        var host = summary.HostEnvironmentInfo;

        var run = new BenchmarkRun
        {
            Id = RunMetadata.SessionId,
            RunAt = DateTimeOffset.UtcNow,
            Label = RunMetadata.Label,
            MachineName = Environment.MachineName,
            OsDescription = SafeGet(() => host.Os.Value?.ToString() ?? string.Empty, RuntimeInformation.OSDescription),
            RuntimeVersion = SafeGet(() => host.RuntimeVersion, RuntimeInformation.FrameworkDescription),
            ProcessorName = SafeGet(() => host.Cpu.Value?.ProcessorName ?? string.Empty, string.Empty),
            ProcessorCount = Environment.ProcessorCount,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            BuildConfiguration = SafeGet(() => host.Configuration, "Unknown"),
            GitCommit = RunMetadata.GitCommit(),
            GitBranch = RunMetadata.GitBranch(),
            TotalDurationMs = summary.TotalTime.TotalMilliseconds
        };

        var results = summary.Reports
            .Select(report => BenchmarkResultMapper.ToResult(ToMeasurement(report)))
            .ToList();

        BenchmarkResultMapper.ApplyBaselineRatios(results);

        run.Results = results;
        return run;
    }

    internal static RawCaseMeasurement ToMeasurement(BenchmarkReport report)
    {
        var descriptor = report.BenchmarkCase.Descriptor;
        var statistics = report.ResultStatistics;
        var gc = report.GcStats;
        var operations = Math.Max(1, gc.TotalOperations);

        return new RawCaseMeasurement(
            Namespace: descriptor.Type.Namespace ?? string.Empty,
            TypeName: descriptor.Type.Name,
            MethodName: descriptor.WorkloadMethod.Name,
            // DisplayInfo comes wrapped in brackets ("[Count=10]"); the full name adds its own.
            Parameters: report.BenchmarkCase.Parameters.DisplayInfo?.Trim('[', ']') ?? string.Empty,
            Categories: string.Join(",", descriptor.Categories),
            IsBaseline: descriptor.Baseline,
            Success: report.Success,
            MeanNs: statistics?.Mean,
            StandardErrorNs: statistics?.StandardError,
            StdDevNs: statistics?.StandardDeviation,
            MedianNs: statistics?.Median,
            MinNs: statistics?.Min,
            MaxNs: statistics?.Max,
            P95Ns: statistics?.Percentiles.P95,
            Gen0: gc.Gen0Collections / (double)operations * 1000,
            Gen1: gc.Gen1Collections / (double)operations * 1000,
            Gen2: gc.Gen2Collections / (double)operations * 1000,
            AllocatedBytes: gc.GetBytesAllocatedPerOperation(report.BenchmarkCase) ?? 0);
    }

    private static string SafeGet(Func<string> getter, string fallback)
    {
        try
        {
            var value = getter();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        catch
        {
            return fallback;
        }
    }
}
