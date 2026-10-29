using BabysittingPool.Application.Notifications;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BabysittingPool.Infrastructure;

internal sealed class RequestStore(BabysittingPoolDbContext db) : IRequestStore
{
    public async Task<CreditAccount?> FindAccountAsync(Guid memberId, CancellationToken cancellationToken)
    {
        // Tracked on purpose: its original Version is what a later save checks against (ADR 0004).
        var record = await db.CreditAccounts.SingleOrDefaultAsync(a => a.MemberId == memberId, cancellationToken);
        return record is null ? null : CreditAccount.Restore(record.Available.Value, record.Escrowed.Value);
    }

    public async Task<HomeLocation?> FindHomeLocationAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking()
            .Where(m => m.Id == memberId)
            .SingleOrDefaultAsync(cancellationToken);
        return member?.ToHome();
    }

    public async Task<Request?> FindRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        // Tracked on purpose: its original Version is what a later save checks against (ADR 0004).
        var record = await db.Requests.SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        return record is null ? null : Request.Restore(
            record.Id, record.RequesterId, record.SitterId,
            new CreditRequestDetails(record.DurationMinutes, record.ChildCount, record.Location, record.Transport, record.Meals),
            record.StartsAt, record.Area, record.Address, record.Cost, record.ClaimCutoffAt, record.PostedAt, record.Status);
    }

    private IDbContextTransaction? _claimTransaction;

    public async Task BeginClaimAsync(CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        // BEGIN IMMEDIATE: a deferred transaction would only take the write lock at the first write, after the check.
        var transaction = connection.BeginTransaction(deferred: false);
        _claimTransaction = await db.Database.UseTransactionAsync(transaction, cancellationToken);
    }

    public async Task<IReadOnlyList<Request>> FindClaimedBySitterAsync(Guid sitterId, CancellationToken cancellationToken)
    {
        var records = await db.Requests.AsNoTracking()
            .Where(r => r.SitterId == sitterId && r.Status == RequestStatus.Claimed)
            .ToListAsync(cancellationToken);
        return records.Select(record => Request.Restore(
            record.Id, record.RequesterId, record.SitterId,
            new CreditRequestDetails(record.DurationMinutes, record.ChildCount, record.Location, record.Transport, record.Meals),
            record.StartsAt, record.Area, record.Address, record.Cost, record.ClaimCutoffAt, record.PostedAt, record.Status)).ToList();
    }

    public async Task<IReadOnlyList<NotificationDelivery>> SaveClaimedAsync(Request request, DateTime now, CancellationToken cancellationToken)
    {
        await SetRequestStateAsync(request, cancellationToken);
        // Same SaveChanges as the Claim, so a lost race or a failure leaves no Notification (ADR 0019).
        var notification = new NotificationRecord
        {
            Id = Guid.NewGuid(),
            MemberId = request.RequesterId,
            Kind = NotificationKinds.RequestClaimed,
            CreatedAt = now,
            RequestId = request.Id,
            SitterId = request.SitterId,
        };
        db.Notifications.Add(notification);
        await SaveAsync(cancellationToken);
        if (_claimTransaction is { } transaction)
        {
            await transaction.CommitAsync(cancellationToken);
            _claimTransaction = null;
        }

        return [new NotificationDelivery(notification.MemberId, notification.ToView())];
    }

    public async Task SaveExpiredAsync(Request request, CreditAccount requester, DateTime now, CancellationToken cancellationToken)
    {
        await SetRequestStateAsync(request, cancellationToken);
        await SetAccountAsync(request.RequesterId, requester, cancellationToken);
        AddLedgerEntry(request.RequesterId, request, LedgerEntryKind.Refund, now);
        await SaveAsync(cancellationToken);
    }

    public async Task SaveCompletedAsync(
        Request request, CreditAccount requester, CreditAccount sitter, DateTime now, CancellationToken cancellationToken)
    {
        var sitterId = request.SitterId ?? throw new InvalidOperationException("A completed Request has a Sitter.");
        await SetRequestStateAsync(request, cancellationToken);
        await SetAccountAsync(request.RequesterId, requester, cancellationToken);
        await SetAccountAsync(sitterId, sitter, cancellationToken);
        AddLedgerEntry(sitterId, request, LedgerEntryKind.Payout, now);
        await SaveAsync(cancellationToken);
    }

    public async Task SaveBurnedAsync(
        Guid memberId, CreditAccount account, decimal burned, DateTime now, CancellationToken cancellationToken)
    {
        await SetAccountAsync(memberId, account, cancellationToken);
        db.LedgerEntries.Add(new LedgerEntryRecord
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Kind = LedgerEntryKind.Burn,
            Amount = Credits.FromDecimal(burned),
            OccurredAt = now,
        });
        await SaveAsync(cancellationToken);
    }

    private async Task SetRequestStateAsync(Request request, CancellationToken cancellationToken)
    {
        var record = await db.Requests.FindAsync([request.Id], cancellationToken)
            ?? throw new InvalidOperationException("The Request must be read before it is saved.");
        record.Status = request.Status;
        record.SitterId = request.SitterId;
        record.Version++;
    }

    private async Task SetAccountAsync(Guid memberId, CreditAccount account, CancellationToken cancellationToken)
    {
        var record = await db.CreditAccounts.FindAsync([memberId], cancellationToken)
            ?? throw new InvalidOperationException("The account must be read before it is saved.");
        record.Available = Credits.FromDecimal(account.Available);
        record.Escrowed = Credits.FromDecimal(account.Escrowed);
        record.Version++;
    }

    private void AddLedgerEntry(Guid memberId, Request request, LedgerEntryKind kind, DateTime now) =>
        db.LedgerEntries.Add(new LedgerEntryRecord
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            RequestId = request.Id,
            Kind = kind,
            Amount = request.Cost,
            OccurredAt = now,
        });

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }

    public async Task<IReadOnlyList<NotificationDelivery>> SaveReleasedAsync(
        Request request, CreditAccount requester, DateTime now, CancellationToken cancellationToken)
    {
        await SetRequestStateAsync(request, cancellationToken);
        if (request.Status is RequestStatus.Cancelled or RequestStatus.Expired)
        {
            await SetAccountAsync(request.RequesterId, requester, cancellationToken);
            AddLedgerEntry(request.RequesterId, request, LedgerEntryKind.Refund, now);
        }

        // A Cancelled Request keeps its Sitter, so only a cancelled claim has someone to tell the Sitter. A withdrawal
        // clears the Sitter and ends Posted or Expired, and it is the Requester who hears of it. Same SaveChanges as
        // the release, so a lost race leaves no Notification (ADR 0019).
        NotificationRecord? notification = null;
        if (request is { Status: RequestStatus.Cancelled, SitterId: { } sitterId })
        {
            notification = ReleaseNotification(sitterId, NotificationKinds.RequestCancelled, request, now);
        }
        else if (request.Status is RequestStatus.Posted or RequestStatus.Expired)
        {
            notification = ReleaseNotification(request.RequesterId, NotificationKinds.RequestWithdrawn, request, now);
        }

        if (notification is not null)
        {
            db.Notifications.Add(notification);
        }

        await SaveAsync(cancellationToken);
        return notification is null ? [] : [new NotificationDelivery(notification.MemberId, notification.ToView())];
    }

    private static NotificationRecord ReleaseNotification(Guid memberId, string kind, Request request, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        MemberId = memberId,
        Kind = kind,
        CreatedAt = now,
        RequestId = request.Id,
        StartsAt = request.StartsAt,
    };

    public async Task<IReadOnlyList<NotificationDelivery>> AddPostedAsync(Request request, CreditAccount requester, DateTime now, CancellationToken cancellationToken)
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

        // Same SaveChanges as the Post, so the fan-out commits or rolls back with it (ADR 0004, ADR 0019).
        var notifications = await PostedNotificationsAsync(request, now, cancellationToken);
        db.Notifications.AddRange(notifications);
        await SaveAsync(cancellationToken);
        return notifications.Select(n => new NotificationDelivery(n.MemberId, n.ToView())).ToList();
    }

    /// <summary>
    /// A Posted Notification for every open Member but the Requester, dormant ones included. Closed Members cannot
    /// sign in, so they get none.
    /// </summary>
    private async Task<List<NotificationRecord>> PostedNotificationsAsync(
        Request request, DateTime now, CancellationToken cancellationToken)
    {
        var audience = await db.Members.AsNoTracking()
            .Where(m => m.ClosedAt == null && m.Id != request.RequesterId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        return audience.Select(memberId => new NotificationRecord
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Kind = NotificationKinds.RequestPosted,
            CreatedAt = now,
            RequestId = request.Id,
            Area = request.Area,
            StartsAt = request.StartsAt,
        }).ToList();
    }
}
