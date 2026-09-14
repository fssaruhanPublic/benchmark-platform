using BenchmarkPlatform.Core.Data;
using BenchmarkPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BenchmarkPlatform.Tests;

/// <summary>
/// A throwaway SQLite file. The real provider is used (rather than the in-memory provider)
/// so the tests exercise the same SQL the dashboard runs against.
/// </summary>
public sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-tests-{Guid.NewGuid():N}.db");
        ConnectionString = $"Data Source={Path}";

        using var db = CreateContext();
        db.Database.Migrate();
    }

    public string Path { get; }

    public string ConnectionString { get; }

    public BenchmarkDbContext CreateContext() => BenchmarkDbContextFactory.Create(ConnectionString);

    public BenchmarkRun SeedRun(DateTimeOffset runAt, string? label, params (string Method, double MeanNs, long Allocated)[] cases)
    {
        var run = new BenchmarkRun
        {
            RunAt = runAt,
            Label = label,
            MachineName = "test-machine",
            OsDescription = "test-os",
            RuntimeVersion = ".NET 10.0",
            ProcessorName = "test-cpu",
            ProcessorCount = 8,
            Architecture = "X64",
            BuildConfiguration = "Release",
            TotalDurationMs = 1234,
            Results = cases.Select(c => new BenchmarkResult
            {
                Namespace = "Sample",
                TypeName = "SuiteBenchmarks",
                MethodName = c.Method,
                FullName = $"SuiteBenchmarks.{c.Method}",
                MeanNs = c.MeanNs,
                MedianNs = c.MeanNs,
                StdDevNs = 1,
                AllocatedBytes = c.Allocated,
                OpsPerSecond = c.MeanNs > 0 ? 1_000_000_000d / c.MeanNs : 0,
                Success = true
            }).ToList()
        };

        using var db = CreateContext();
        db.Runs.Add(run);
        db.SaveChanges();

        return run;
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
        catch (IOException)
        {
            // Leftover temp files are harmless.
        }
    }
}
