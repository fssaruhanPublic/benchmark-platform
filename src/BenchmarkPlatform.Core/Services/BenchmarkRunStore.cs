using BenchmarkPlatform.Core.Data;
using BenchmarkPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BenchmarkPlatform.Core.Services;

/// <summary>
/// Write-side entry point used by the BenchmarkDotNet exporter, which runs outside of DI.
/// </summary>
public static class BenchmarkRunStore
{
    /// <summary>
    /// Inserts the run, or — when a run with the same id already exists — appends the results to it.
    /// BenchmarkDotNet exports one summary per benchmark class, and a single process invocation
    /// should still show up as a single run in the dashboard.
    /// </summary>
    /// <returns>The total number of results stored for the run.</returns>
    public static int Save(BenchmarkRun run, string? connectionString = null)
    {
        if (connectionString is null)
        {
            var databasePath = BenchmarkDbPath.Resolve();
            BenchmarkDbPath.EnsureDirectoryExists(databasePath);
            connectionString = $"Data Source={databasePath}";
        }

        using var db = BenchmarkDbContextFactory.Create(connectionString);
        db.Database.Migrate();

        var existing = db.Runs.AsNoTracking().FirstOrDefault(r => r.Id == run.Id);

        if (existing is null)
        {
            db.Runs.Add(run);
            db.SaveChanges();
            return run.Results.Count;
        }

        foreach (var result in run.Results)
        {
            result.RunId = run.Id;
            result.Run = null;
            db.Results.Add(result);
        }

        db.SaveChanges();

        var totalDuration = existing.TotalDurationMs + run.TotalDurationMs;
        db.Runs
            .Where(r => r.Id == run.Id)
            .ExecuteUpdate(setters => setters
                .SetProperty(r => r.RunAt, run.RunAt)
                .SetProperty(r => r.TotalDurationMs, totalDuration));

        return db.Results.Count(r => r.RunId == run.Id);
    }
}
