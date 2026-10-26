using BenchmarkPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BenchmarkPlatform.Core.Data;

public class BenchmarkDbContext : DbContext
{
    public BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : base(options)
    {
    }

    /// <summary>For provider-specific subclasses (see <see cref="SqlServerBenchmarkDbContext"/>).</summary>
    protected BenchmarkDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<BenchmarkRun> Runs => Set<BenchmarkRun>();

    public DbSet<BenchmarkResult> Results => Set<BenchmarkResult>();

    public DbSet<ScenarioRun> ScenarioRuns => Set<ScenarioRun>();

    public DbSet<ScenarioCase> ScenarioCases => Set<ScenarioCase>();

    public DbSet<ScenarioMetric> ScenarioMetrics => Set<ScenarioMetric>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureScenarios(modelBuilder);

        modelBuilder.Entity<BenchmarkRun>(entity =>
        {
            entity.HasKey(e => e.Id);

            // SQLite cannot sort DateTimeOffset columns, so runs are stored as UTC ticks.
            entity.Property(e => e.RunAt).HasConversion(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero));

            entity.HasIndex(e => e.RunAt);
            entity.Property(e => e.Label).HasMaxLength(200);
            entity.Property(e => e.MachineName).HasMaxLength(200);
            entity.Property(e => e.OsDescription).HasMaxLength(300);
            entity.Property(e => e.RuntimeVersion).HasMaxLength(200);
            entity.Property(e => e.ProcessorName).HasMaxLength(300);
            entity.Property(e => e.Architecture).HasMaxLength(50);
            entity.Property(e => e.BuildConfiguration).HasMaxLength(50);
            entity.Property(e => e.GitCommit).HasMaxLength(60);
            entity.Property(e => e.GitBranch).HasMaxLength(200);
        });

        modelBuilder.Entity<BenchmarkResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FullName, e.RunId });
            entity.Property(e => e.Namespace).HasMaxLength(300);
            entity.Property(e => e.TypeName).HasMaxLength(300);
            entity.Property(e => e.MethodName).HasMaxLength(300);
            entity.Property(e => e.FullName).HasMaxLength(600);
            entity.Property(e => e.Parameters).HasMaxLength(600);
            entity.Property(e => e.Categories).HasMaxLength(400);

            entity.HasOne(e => e.Run)
                .WithMany(r => r.Results)
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureScenarios(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScenarioRun>(entity =>
        {
            entity.ToTable("ScenarioRuns");
            entity.HasKey(e => e.Id);

            // Same reason as BenchmarkRun.RunAt: SQLite cannot order DateTimeOffset.
            entity.Property(e => e.StartedAt).HasConversion(
                value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
            entity.Property(e => e.FinishedAt).HasConversion(
                value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
            entity.Property(e => e.ImportedAt).HasConversion(
                value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));

            entity.HasIndex(e => new { e.Suite, e.StartedAt });
            entity.HasIndex(e => e.Kind);
            entity.Property(e => e.Kind).HasMaxLength(40);
            entity.Property(e => e.Suite).HasMaxLength(200);
            entity.Property(e => e.Label).HasMaxLength(200);
            entity.Property(e => e.GitCommit).HasMaxLength(60);
            entity.Property(e => e.GitBranch).HasMaxLength(200);
            entity.Property(e => e.MachineName).HasMaxLength(200);
            entity.Property(e => e.Target).HasMaxLength(500);

            entity.HasMany(e => e.Cases)
                .WithOne(c => c.Run)
                .HasForeignKey(c => c.RunId)
                .OnDelete(DeleteBehavior.Cascade);

        });

        modelBuilder.Entity<ScenarioCase>(entity =>
        {
            entity.ToTable("ScenarioCases");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RunId, e.Name });
            entity.Property(e => e.Name).HasMaxLength(400);
            entity.Property(e => e.Group).HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(2000);

            entity.HasMany(e => e.Metrics)
                .WithOne()
                .HasForeignKey(m => m.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScenarioMetric>(entity =>
        {
            entity.ToTable("ScenarioMetrics");
            entity.HasKey(e => e.Id);

            // Every metric carries its RunId, run-level (CaseId null) or not, so the
            // history query can filter by run without joining through cases. SQLite
            // allows the resulting two cascade paths (run → metric, run → case → metric).
            entity.HasOne<ScenarioRun>()
                .WithMany()
                .HasForeignKey(m => m.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.RunId, e.CaseId, e.Name });
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Unit).HasMaxLength(40);
        });
    }
}
