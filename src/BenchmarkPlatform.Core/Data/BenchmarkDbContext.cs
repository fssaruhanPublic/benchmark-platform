using BenchmarkPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BenchmarkPlatform.Core.Data;

public class BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : DbContext(options)
{
    public DbSet<BenchmarkRun> Runs => Set<BenchmarkRun>();

    public DbSet<BenchmarkResult> Results => Set<BenchmarkResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
}
