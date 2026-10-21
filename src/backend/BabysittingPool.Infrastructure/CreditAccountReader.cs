using BabysittingPool.Application.Credits;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

internal sealed class CreditAccountReader(BabysittingPoolDbContext db) : ICreditAccountReader
{
    public async Task<AccountRead?> FindAsync(
        Guid memberId, LedgerCursor? after, int take, CancellationToken cancellationToken)
    {
        var account = await db.CreditAccounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.MemberId == memberId, cancellationToken);
        if (account is null) return null;

        var ledger = db.LedgerEntries.AsNoTracking().Where(e => e.MemberId == memberId);
        if (after is not null)
        {
            ledger = ledger.Where(e => e.OccurredAt < after.OccurredAt
                || (e.OccurredAt == after.OccurredAt && e.Id.CompareTo(after.Id) < 0));
        }

        var entries = await ledger
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new AccountRead(
            account.Available.QuarterCredits,
            account.Escrowed.QuarterCredits,
            entries.Select(e => new LedgerEntryView(e.Id, e.Kind.ToString(), e.Amount.QuarterCredits, e.OccurredAt)).ToList());
    }
}
