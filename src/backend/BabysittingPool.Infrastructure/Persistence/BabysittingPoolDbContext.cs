using BabysittingPool.Domain.Credits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BabysittingPool.Infrastructure.Persistence;

public sealed class BabysittingPoolDbContext(DbContextOptions<BabysittingPoolDbContext> options) : DbContext(options)
{
    public DbSet<CreditAccountRecord> CreditAccounts => Set<CreditAccountRecord>();
    public DbSet<LedgerEntryRecord> LedgerEntries => Set<LedgerEntryRecord>();
    public DbSet<MemberRecord> Members => Set<MemberRecord>();
    public DbSet<InvitationRecord> Invitations => Set<InvitationRecord>();
    public DbSet<SignInLinkRecord> SignInLinks => Set<SignInLinkRecord>();
    public DbSet<SessionRecord> Sessions => Set<SessionRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Credits>().HaveConversion<CreditsConverter>();
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
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

        modelBuilder.Entity<MemberRecord>(member =>
        {
            member.ToTable("Members");
            member.HasKey(m => m.Id);
            member.Property(m => m.Language).HasDefaultValue("en-US");
            // A closed Member's address may be invited again as a new Member (spec §2A).
            member.HasIndex(m => m.Email).IsUnique().HasFilter("ClosedAt IS NULL");
        });

        modelBuilder.Entity<InvitationRecord>(invitation =>
        {
            invitation.ToTable("Invitations");
            invitation.HasKey(i => i.Id);
            invitation.HasIndex(i => i.SecretHash).IsUnique();
            invitation.Property(i => i.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<SignInLinkRecord>(link =>
        {
            link.ToTable("SignInLinks");
            link.HasKey(l => l.Id);
            link.HasIndex(l => l.SecretHash).IsUnique();
            link.Property(l => l.Version).IsConcurrencyToken();
            link.HasOne<MemberRecord>().WithMany().HasForeignKey(l => l.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SessionRecord>(session =>
        {
            session.ToTable("Sessions");
            session.HasKey(s => s.Id);
            session.HasIndex(s => s.SecretHash).IsUnique();
            session.HasOne<MemberRecord>().WithMany().HasForeignKey(s => s.MemberId).OnDelete(DeleteBehavior.Restrict);
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

    private sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        value => value,
        value => value == null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
