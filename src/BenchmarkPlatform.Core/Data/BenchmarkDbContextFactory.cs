using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BenchmarkPlatform.Core.Data;

/// <summary>
/// Used by <c>dotnet ef</c> at design time and by the exporter, which has no DI container.
/// </summary>
public class BenchmarkDbContextFactory : IDesignTimeDbContextFactory<BenchmarkDbContext>
{
    public BenchmarkDbContext CreateDbContext(string[] args) => Create(BenchmarkDbPath.ConnectionString());

    public static BenchmarkDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<BenchmarkDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new BenchmarkDbContext(options);
    }
}
