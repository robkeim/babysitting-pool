using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure.Persistence;

/// <summary>
/// Rate-limit counters (ADR 0014), in their own SQLite file beside the main database so a counter write never
/// queues behind a Ledger transaction. Losing the file only resets the windows.
/// </summary>
public sealed class RateLimitDbContext(DbContextOptions<RateLimitDbContext> options) : DbContext(options)
{
    public DbSet<RateLimitCounterRecord> Counters => Set<RateLimitCounterRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<RateLimitCounterRecord>(counter =>
        {
            counter.ToTable("RateLimitCounters");
            counter.HasKey(c => c.Key);
            counter.HasIndex(c => c.WindowEnd);
        });
}
