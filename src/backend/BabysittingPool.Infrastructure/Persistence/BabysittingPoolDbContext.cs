using BabysittingPool.Domain.Credits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BabysittingPool.Infrastructure.Persistence;

public sealed class BabysittingPoolDbContext(DbContextOptions<BabysittingPoolDbContext> options) : DbContext(options)
{
    public DbSet<CreditAccountRecord> CreditAccounts => Set<CreditAccountRecord>();
    public DbSet<LedgerEntryRecord> LedgerEntries => Set<LedgerEntryRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Credits>().HaveConversion<CreditsConverter>();
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<LedgerEntryKind>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CreditAccountRecord>(account =>
        {
            account.ToTable("CreditAccounts");
            account.HasKey(a => a.MemberId);
            account.Property(a => a.Available).HasColumnName("AvailableQuarterCredits");
            account.Property(a => a.Escrowed).HasColumnName("EscrowedQuarterCredits");
            account.Property(a => a.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<LedgerEntryRecord>(entry =>
        {
            entry.ToTable("LedgerEntries");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Amount).HasColumnName("AmountQuarterCredits");
            entry.HasOne<CreditAccountRecord>().WithMany().HasForeignKey(e => e.MemberId).OnDelete(DeleteBehavior.Restrict);
            entry.HasIndex(e => new { e.MemberId, e.OccurredAt, e.Id });
        });
    }

    private sealed class CreditsConverter() : ValueConverter<Credits, long>(
        credits => credits.QuarterCredits,
        quarters => Credits.FromQuarterCredits(quarters));

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
