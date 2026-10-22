using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Support;

/// <summary>Arranges Credit Accounts directly in the database for read-side tests.</summary>
internal static class AccountSeeder
{
    /// <summary>A Credit Account whose Ledger holds the given Grants, one hour apart starting at the clock.</summary>
    public static async Task<Guid> SeedAccountAsync(
        IServiceProvider services, TimeProvider clock, Credits available, Credits escrowed, params Credits[] grants)
    {
        var memberId = Guid.NewGuid();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BabysittingPoolDbContext>();
        db.CreditAccounts.Add(new CreditAccountRecord { MemberId = memberId, Available = available, Escrowed = escrowed });
        for (var i = 0; i < grants.Length; i++)
        {
            db.LedgerEntries.Add(new LedgerEntryRecord
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                Kind = LedgerEntryKind.Grant,
                Amount = grants[i],
                OccurredAt = clock.GetUtcNow().UtcDateTime.AddHours(i),
            });
        }
        await db.SaveChangesAsync();
        return memberId;
    }

    /// <summary>An account whose Ledger holds <paramref name="count"/> Grants at the very same instant.</summary>
    public static async Task<Guid> SeedSimultaneousGrantsAsync(IServiceProvider services, TimeProvider clock, int count)
    {
        var memberId = Guid.NewGuid();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BabysittingPoolDbContext>();
        db.CreditAccounts.Add(new CreditAccountRecord
        {
            MemberId = memberId,
            Available = Credits.FromQuarterCredits(count),
            Escrowed = Credits.FromQuarterCredits(0),
        });
        for (var i = 0; i < count; i++)
        {
            db.LedgerEntries.Add(new LedgerEntryRecord
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                Kind = LedgerEntryKind.Grant,
                Amount = Credits.FromQuarterCredits(1),
                OccurredAt = clock.GetUtcNow().UtcDateTime,
            });
        }
        await db.SaveChangesAsync();
        return memberId;
    }
}
