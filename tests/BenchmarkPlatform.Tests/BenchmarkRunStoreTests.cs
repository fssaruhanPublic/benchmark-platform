using BenchmarkPlatform.Core.Entities;
using BenchmarkPlatform.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BenchmarkPlatform.Tests;

public class BenchmarkRunStoreTests
{
    private static BenchmarkRun Run(Guid id, string method, double totalDurationMs = 100) => new()
    {
        Id = id,
        RunAt = DateTimeOffset.UtcNow,
        Label = "session",
        MachineName = "test-machine",
        OsDescription = "test-os",
        RuntimeVersion = ".NET 10.0",
        ProcessorName = "test-cpu",
        ProcessorCount = 8,
        Architecture = "X64",
        BuildConfiguration = "Release",
        TotalDurationMs = totalDurationMs,
        Results =
        [
            new BenchmarkResult
            {
                TypeName = "Suite",
                MethodName = method,
                FullName = $"Suite.{method}",
                MeanNs = 100,
                Success = true
            }
        ]
    };

    [Fact]
    public void Save_InsertsANewRun()
    {
        using var temp = new TempDatabase();

        var stored = BenchmarkRunStore.Save(Run(Guid.NewGuid(), "First"), temp.ConnectionString);

        Assert.Equal(1, stored);
    }

    [Fact]
    public void Save_AppendsToAnExistingRunWithTheSameId()
    {
        using var temp = new TempDatabase();
        var sessionId = Guid.NewGuid();

        BenchmarkRunStore.Save(Run(sessionId, "First", 100), temp.ConnectionString);
        var stored = BenchmarkRunStore.Save(Run(sessionId, "Second", 250), temp.ConnectionString);

        Assert.Equal(2, stored);

        using var db = temp.CreateContext();
        var run = db.Runs.Include(r => r.Results).Single();

        Assert.Equal(sessionId, run.Id);
        Assert.Equal(350, run.TotalDurationMs);
        Assert.Equal(["First", "Second"], run.Results.Select(r => r.MethodName).Order());
    }
}
