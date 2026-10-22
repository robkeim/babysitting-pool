using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Infrastructure;

/// <summary>Creates Members directly, for Development and E2E only; Production only ever uses Invitations.</summary>
public static class MemberSeeder
{
    /// <summary>
    /// Adds a Member whose Credit Account is the sum of <paramref name="grants"/>, with one Grant Ledger entry per
    /// amount, one hour apart starting now. Does nothing if the address already belongs to a Member.
    /// </summary>
    public static async Task<Guid> SeedMemberAsync(
        this IServiceProvider services,
        string email,
        string displayName,
        bool isAdmin,
        IReadOnlyList<Credits> grants,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BabysittingPoolDbContext>();
        var existing = await db.Members.SingleOrDefaultAsync(m => m.Email == email && m.ClosedAt == null, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var memberId = Guid.NewGuid();
        db.Members.Add(new MemberRecord
        {
            Id = memberId,
            Email = email,
            DisplayName = displayName,
            IsAdmin = isAdmin,
            CreatedAt = now,
        });
        db.CreditAccounts.Add(new CreditAccountRecord
        {
            MemberId = memberId,
            Available = Credits.FromQuarterCredits(grants.Sum(g => g.QuarterCredits)),
            Escrowed = Credits.FromQuarterCredits(0),
        });
        for (var i = 0; i < grants.Count; i++)
        {
            db.LedgerEntries.Add(new LedgerEntryRecord
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                Kind = LedgerEntryKind.Grant,
                Amount = grants[i],
                OccurredAt = now.AddHours(i),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return memberId;
    }

    /// <summary>
    /// Development: one Admin and three Members with different balances (ADR 0011), then logs the Admin's
    /// sign-in link through the outbox. Safe to run on every start.
    /// </summary>
    public static async Task SeedDevelopmentAsync(
        this IServiceProvider services, string adminEmail, CancellationToken cancellationToken = default)
    {
        var admin = Credits.FromDecimal(CreditAccount.StartingBalance);
        await services.SeedMemberAsync(adminEmail, "Dev Admin", true, [admin], cancellationToken);
        await services.SeedMemberAsync("ada@example.test", "Ada (10.0)", false, [admin], cancellationToken);
        await services.SeedMemberAsync("ben@example.test", "Ben (0.0)", false, [], cancellationToken);
        await services.SeedMemberAsync(
            "cleo@example.test", "Cleo (25.5)", false, [admin, Credits.FromDecimal(15.5m)], cancellationToken);

        await using var scope = services.CreateAsyncScope();
        if (EmailAddress.TryParse(adminEmail, out var address))
        {
            await scope.ServiceProvider.GetRequiredService<RequestSignInLinkHandler>().HandleAsync(address, cancellationToken);
        }
    }
}
