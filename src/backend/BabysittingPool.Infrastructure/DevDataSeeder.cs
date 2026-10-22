using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Infrastructure;

/// <summary>Development-only seed: one Member holding a freshly opened Credit Account.</summary>
public static class DevDataSeeder
{
    public static async Task SeedMemberAsync(
        this IServiceProvider services, Guid memberId, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BabysittingPoolDbContext>();
        if (await db.CreditAccounts.AnyAsync(a => a.MemberId == memberId, cancellationToken))
        {
            return;
        }

        var account = CreditAccount.Open();
        var available = Credits.FromDecimal(account.Available);
        db.CreditAccounts.Add(new CreditAccountRecord
        {
            MemberId = memberId,
            Available = available,
            Escrowed = Credits.FromDecimal(account.Escrowed),
        });
        db.LedgerEntries.Add(new LedgerEntryRecord
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Kind = LedgerEntryKind.Grant,
            Amount = available,
            OccurredAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
