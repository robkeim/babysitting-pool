using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Infrastructure;

/// <summary>E2E-only seed: a Member whose Credit Account is built from the given Ledger Grants.</summary>
public static class E2eDataSeeder
{
    /// <summary>
    /// Opens an account holding the sum of <paramref name="grants"/>, with one Grant Ledger entry per amount,
    /// one hour apart starting at the current time.
    /// </summary>
    public static async Task<Guid> SeedMemberWithGrantsAsync(
        this IServiceProvider services, IReadOnlyList<Credits> grants, CancellationToken cancellationToken = default)
    {
        var memberId = Guid.NewGuid();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BabysittingPoolDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

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
}
