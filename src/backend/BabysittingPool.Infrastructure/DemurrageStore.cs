using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

internal sealed class DemurrageStore(BabysittingPoolDbContext db) : IDemurrageStore, IDemurrageReader
{
    public async Task<IReadOnlyList<Guid>> ListChargeableAsync(CancellationToken cancellationToken) =>
        await db.Members.AsNoTracking()
            .Where(m => m.ClosedAt == null)
            .Join(db.CreditAccounts.AsNoTracking(), m => m.Id, a => a.MemberId, (m, a) => new { m.Id, a.Available })
            .Where(x => x.Available != Credits.FromQuarterCredits(0))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<DemurrageCandidate?> FindAsync(Guid memberId, CancellationToken cancellationToken)
    {
        // Tracked on purpose: its original Version is what a later save checks against (ADR 0004).
        var record = await db.CreditAccounts.SingleOrDefaultAsync(a => a.MemberId == memberId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var memberSince = await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId).Select(m => m.CreatedAt).SingleAsync(cancellationToken);
        var lastCreditAt = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.MemberId == memberId
                && (e.Kind == LedgerEntryKind.Grant || e.Kind == LedgerEntryKind.Refund || e.Kind == LedgerEntryKind.Payout))
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => (DateTime?)e.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
        var charged = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.MemberId == memberId && e.Boundary != null)
            .Select(e => e.Boundary!.Value)
            .ToListAsync(cancellationToken);
        var completed = await db.Requests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Completed && (r.RequesterId == memberId || r.SitterId == memberId))
            .Select(r => r.StartsAt)
            .ToListAsync(cancellationToken);

        return new DemurrageCandidate(
            CreditAccount.Restore(record.Available.Value, record.Escrowed.Value), memberSince, lastCreditAt, charged, completed);
    }

    public async Task SaveChargedAsync(
        Guid memberId, CreditAccount account, IReadOnlyList<DemurrageCharge> charges, DateTime now, CancellationToken cancellationToken)
    {
        var record = await db.CreditAccounts.FindAsync([memberId], cancellationToken)
            ?? throw new InvalidOperationException("The account must be read before it is saved.");
        record.Available = Credits.FromDecimal(account.Available);
        record.Escrowed = Credits.FromDecimal(account.Escrowed);
        record.Version++;

        foreach (var charge in charges)
        {
            // Burned: no recipient and no Request. The unique (Member, Boundary) index makes a double charge impossible.
            db.LedgerEntries.Add(new LedgerEntryRecord
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                Kind = LedgerEntryKind.Demurrage,
                Amount = Credits.FromDecimal(charge.Amount),
                OccurredAt = now,
                Boundary = charge.Boundary,
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }
}
