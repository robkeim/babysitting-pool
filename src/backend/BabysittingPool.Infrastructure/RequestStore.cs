using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

internal sealed class RequestStore(BabysittingPoolDbContext db) : IRequestStore
{
    public async Task<CreditAccount?> FindAccountAsync(Guid memberId, CancellationToken cancellationToken)
    {
        // Tracked on purpose: its original Version is what a later save checks against (ADR 0004).
        var record = await db.CreditAccounts.SingleOrDefaultAsync(a => a.MemberId == memberId, cancellationToken);
        return record is null ? null : CreditAccount.Restore(record.Available.Value, record.Escrowed.Value);
    }

    public async Task AddPostedAsync(Request request, CreditAccount requester, DateTime now, CancellationToken cancellationToken)
    {
        var account = await db.CreditAccounts.FindAsync([request.RequesterId], cancellationToken)
            ?? throw new InvalidOperationException("The Requester's account must be read before it is saved.");
        account.Available = Credits.FromDecimal(requester.Available);
        account.Escrowed = Credits.FromDecimal(requester.Escrowed);
        account.Version++;

        db.Requests.Add(new RequestRecord
        {
            Id = request.Id,
            RequesterId = request.RequesterId,
            SitterId = request.SitterId,
            StartsAt = request.StartsAt,
            DurationMinutes = request.Details.DurationMinutes,
            ChildCount = request.Details.ChildCount,
            Location = request.Details.Location,
            Transport = request.Details.Transport,
            Meals = request.Details.Meals,
            Area = request.Area,
            Address = request.Address,
            Cost = request.Cost,
            ClaimCutoffAt = request.ClaimCutoffAt,
            PostedAt = request.PostedAt,
            Status = request.Status,
        });
        db.LedgerEntries.Add(new LedgerEntryRecord
        {
            Id = Guid.NewGuid(),
            MemberId = request.RequesterId,
            RequestId = request.Id,
            Kind = LedgerEntryKind.Escrow,
            Amount = request.Cost,
            OccurredAt = now,
        });

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
