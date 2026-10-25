using BabysittingPool.Application;
using BabysittingPool.Application.Credits;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

internal sealed class CreditAccountReader(BabysittingPoolDbContext db) : ICreditAccountReader
{
    public async Task<AccountRead?> FindAsync(
        Guid memberId, PageRequest page, CancellationToken cancellationToken)
    {
        var account = await db.CreditAccounts.AsNoTracking()
            .SingleOrDefaultAsync(a => a.MemberId == memberId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.MemberId == memberId)
            .ToKeysetPageAsync(page, e => e.OccurredAt, e => e.Id, descending: true, cancellationToken);

        return new AccountRead(
            account.Available.QuarterCredits,
            account.Escrowed.QuarterCredits,
            entries.Select(e => new LedgerEntryView(e.Id, e.Kind, e.Amount.QuarterCredits, e.OccurredAt)).ToList());
    }
}
