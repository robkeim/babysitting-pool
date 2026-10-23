using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BabysittingPool.Infrastructure.Persistence;

public sealed class BabysittingPoolDbContext(DbContextOptions<BabysittingPoolDbContext> options) : DbContext(options)
{
    public DbSet<CreditAccountRecord> CreditAccounts => Set<CreditAccountRecord>();
    public DbSet<LedgerEntryRecord> LedgerEntries => Set<LedgerEntryRecord>();
    public DbSet<RequestRecord> Requests => Set<RequestRecord>();
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
        configurationBuilder.Properties<RequestStatus>().HaveConversion<string>();
        configurationBuilder.Properties<BabysittingLocation>().HaveConversion<string>();
        configurationBuilder.Properties<TransportOption>().HaveConversion<string>();
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

        modelBuilder.Entity<RequestRecord>(request =>
        {
            request.ToTable("Requests");
            request.HasKey(r => r.Id);
            request.Property(r => r.Cost).HasColumnName("CostQuarterCredits");
            request.Property(r => r.Version).IsConcurrencyToken();
            request.HasOne<MemberRecord>().WithMany().HasForeignKey(r => r.RequesterId).OnDelete(DeleteBehavior.Restrict);
            request.HasOne<MemberRecord>().WithMany().HasForeignKey(r => r.SitterId).OnDelete(DeleteBehavior.Restrict);
            request.HasIndex(r => new { r.RequesterId, r.PostedAt, r.Id });
        });

        modelBuilder.Entity<LedgerEntryRecord>(entry =>
        {
            entry.ToTable("LedgerEntries");
            entry.HasKey(e => e.Id);
            entry.Property(e => e.Amount).HasColumnName("AmountQuarterCredits");
            entry.HasOne<CreditAccountRecord>().WithMany().HasForeignKey(e => e.MemberId).OnDelete(DeleteBehavior.Restrict);
            entry.HasIndex(e => new { e.MemberId, e.OccurredAt, e.Id });
            // Each Request's Escrow, Refund and Payout can be recorded at most once. No foreign key to Requests:
            // adding one would make SQLite rebuild this table and drop its append-only triggers.
            entry.HasIndex(e => new { e.RequestId, e.Kind }).IsUnique().HasFilter("RequestId IS NOT NULL");
            // A weekly boundary is charged at most once per Member (ADR 0007).
            entry.HasIndex(e => new { e.MemberId, e.Boundary }).IsUnique().HasFilter("Boundary IS NOT NULL");
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
