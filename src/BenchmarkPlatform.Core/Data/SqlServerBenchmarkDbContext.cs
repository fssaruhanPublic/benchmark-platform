using BenchmarkPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BenchmarkPlatform.Core.Data;

/// <summary>
/// The same model on SQL Server (used for a hosted dashboard, e.g. Azure SQL). SQLite stays the
/// default for local use; the two providers keep separate migration sets because the generated
/// DDL differs.
///
/// One model difference: SQL Server rejects multiple cascade paths into one table. A metric is
/// reachable from its run directly and through its case, so the case → metric link does not
/// cascade here. Deleting a run still removes everything (run → case and run → metric cascade).
/// </summary>
public class SqlServerBenchmarkDbContext(DbContextOptions<SqlServerBenchmarkDbContext> options)
    : BenchmarkDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ScenarioCase>()
            .HasMany(c => c.Metrics)
            .WithOne()
            .HasForeignKey(m => m.CaseId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

/// <summary>Design-time factory so <c>dotnet ef</c> can generate the SQL Server migrations.</summary>
public class SqlServerBenchmarkDbContextFactory : IDesignTimeDbContextFactory<SqlServerBenchmarkDbContext>
{
    public SqlServerBenchmarkDbContext CreateDbContext(string[] args) =>
        Create(Environment.GetEnvironmentVariable("BENCHMARK_SQLSERVER")
               ?? "Server=(localdb)\\MSSQLLocalDB;Database=BenchmarkPlatformDesign;Trusted_Connection=True;");

    public static SqlServerBenchmarkDbContext Create(string connectionString) =>
        new(new DbContextOptionsBuilder<SqlServerBenchmarkDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options);
}
