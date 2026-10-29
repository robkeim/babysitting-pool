using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Identity;
using BabysittingPool.Application.Notifications;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BabysittingPool.Application.Requests;

/// <summary>What one Settle run changed; failed rows are retried on the next run (ADR 0007).</summary>
public sealed record SettleResult(int Expired, int Completed, int Charged, int Failed);

/// <summary>
/// Applies the time-driven changes that have become due: Expiry, then Payout, then Demurrage (spec §5, §6, ADR 0007). Idempotent, because
/// only Requests still Posted or Claimed past their trigger are selected, and each Request is settled in its own
/// transaction so one bad row never blocks the rest. Demurrage is idempotent per (Member, boundary) by a unique index.
/// </summary>
public sealed partial class SettleHandler(
    IRequestReader reader,
    IDemurrageReader demurrageReader,
    IServiceScopeFactory scopes,
    CoopSettings settings,
    TimeProvider clock,
    ILogger<SettleHandler> logger)
{
    public async Task<SettleResult> HandleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        int expired = 0, completed = 0, charged = 0, failed = 0;

        foreach (var id in await reader.ListDueForExpiryAsync(now, cancellationToken))
        {
            if (await SettleAsync(id, ExpireAsync, "Expiry", cancellationToken))
            {
                expired++;
            }
            else
            {
                failed++;
            }
        }

        foreach (var id in await reader.ListDueForPayoutAsync(now, cancellationToken))
        {
            if (await SettleAsync(id, CompleteAsync, "Payout", cancellationToken))
            {
                completed++;
            }
            else
            {
                failed++;
            }
        }

        foreach (var memberId in await demurrageReader.ListChargeableAsync(cancellationToken))
        {
            var outcome = await ChargeAsync(memberId, cancellationToken);
            if (outcome is null)
            {
                failed++;
            }
            else if (outcome.Value)
            {
                charged++;
            }
        }

        await SweepNotificationsAsync(now, cancellationToken);

        var result = new SettleResult(expired, completed, charged, failed);
        LogRan(result.Expired, result.Completed, result.Charged, result.Failed);
        return result;
    }

    /// <summary>
    /// Deletes Notifications whose Request has started, and any past their retention as a safety net for ones nobody
    /// dismissed (ADR 0019). Settle itself
    /// writes none (ADR 0007). A failure is logged and retried next run; it never blocks the rest.
    /// </summary>
    private async Task SweepNotificationsAsync(DateTime now, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        try
        {
            var store = scope.ServiceProvider.GetRequiredService<INotificationStore>();
            var deleted = await store.DeleteForStartedRequestsAsync(now, cancellationToken)
                + await store.DeleteOlderThanAsync(now - settings.NotificationRetention, cancellationToken);
            LogSwept(deleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSweepFailed(ex);
        }
    }

    private async Task<bool> SettleAsync(
        Guid requestId,
        Func<IRequestStore, Guid, DateTime, CancellationToken, Task> settle,
        string step,
        CancellationToken cancellationToken)
    {
        // A scope per Request is its own transaction and its own tracked state (ADR 0004).
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IRequestStore>();
        try
        {
            await settle(store, requestId, clock.GetUtcNow().UtcDateTime, cancellationToken);
            LogSettled(step, requestId);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            LogConflicted(step, requestId);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(ex, step, requestId);
            return false;
        }
    }

    /// <summary>Whether the Member was charged, or null if the attempt failed and is left for the next run.</summary>
    private async Task<bool?> ChargeAsync(Guid memberId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDemurrageStore>();
        try
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var candidate = await store.FindAsync(memberId, cancellationToken);
            if (candidate is null)
            {
                return false;
            }

            // Boundaries from the account's creation, or from its last credit if later: a boundary at which the
            // account was empty is never charged retroactively once credits arrive.
            var since = candidate.LastCreditAt is { } credited && credited > candidate.MemberSince
                ? credited
                : candidate.MemberSince;
            var schedule = new DemurrageSchedule(settings.DemurrageDay, settings.DemurrageTime, settings.TimeZone);
            var boundaries = schedule.BoundariesBetween(since, now).Except(candidate.ChargedBoundaries);

            var charges = Demurrage.Apply(candidate.Account, candidate.MemberSince, candidate.CompletedStartTimes, boundaries);
            if (charges.Count == 0)
            {
                return false;
            }

            await store.SaveChargedAsync(memberId, candidate.Account, charges, now, cancellationToken);
            LogSettled("Demurrage", memberId);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            LogConflicted("Demurrage", memberId);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(ex, "Demurrage", memberId);
            return null;
        }
    }

    private static async Task ExpireAsync(
        IRequestStore store, Guid requestId, DateTime now, CancellationToken cancellationToken)
    {
        var request = await FindRequestAsync(store, requestId, cancellationToken);
        var requester = await FindAccountAsync(store, request.RequesterId, cancellationToken);

        request.Expire(requester, now);
        await store.SaveExpiredAsync(request, requester, now, cancellationToken);
    }

    private static async Task CompleteAsync(
        IRequestStore store, Guid requestId, DateTime now, CancellationToken cancellationToken)
    {
        var request = await FindRequestAsync(store, requestId, cancellationToken);
        var requester = await FindAccountAsync(store, request.RequesterId, cancellationToken);
        var sitter = await FindAccountAsync(
            store, request.SitterId ?? throw new InvalidOperationException("A Claimed Request has a Sitter."), cancellationToken);

        request.Complete(requester, sitter, now);
        await store.SaveCompletedAsync(request, requester, sitter, now, cancellationToken);
    }

    private static async Task<Request> FindRequestAsync(IRequestStore store, Guid id, CancellationToken cancellationToken) =>
        await store.FindRequestAsync(id, cancellationToken) ?? throw new InvalidOperationException("The Request vanished.");

    private static async Task<CreditAccount> FindAccountAsync(IRequestStore store, Guid memberId, CancellationToken cancellationToken) =>
        await store.FindAccountAsync(memberId, cancellationToken) ?? throw new InvalidOperationException("The Member has no Credit Account.");

    [LoggerMessage(Level = LogLevel.Information, Message = "SettleRan {Expired} {Completed} {Charged} {Failed}")]
    private partial void LogRan(int expired, int completed, int charged, int failed);

    [LoggerMessage(Level = LogLevel.Information, Message = "NotificationsSwept {Deleted}")]
    private partial void LogSwept(int deleted);

    [LoggerMessage(Level = LogLevel.Error, Message = "NotificationSweepFailed")]
    private partial void LogSweepFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Settled {Step} {Id}")]
    private partial void LogSettled(string step, Guid id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SettleConflicted {Step} {Id}")]
    private partial void LogConflicted(string step, Guid id);

    [LoggerMessage(Level = LogLevel.Error, Message = "SettleFailed {Step} {Id}")]
    private partial void LogFailed(Exception exception, string step, Guid id);
}
