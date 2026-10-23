using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BabysittingPool.Application.Requests;

/// <summary>What one Settle run changed; failed rows are retried on the next run (ADR 0007).</summary>
public sealed record SettleResult(int Expired, int Completed, int Failed);

/// <summary>
/// Applies the time-driven changes that have become due: Expiry, then Payout (spec §6, ADR 0007). Idempotent, because
/// only Requests still Posted or Claimed past their trigger are selected, and each Request is settled in its own
/// transaction so one bad row never blocks the rest.
/// </summary>
public sealed partial class SettleHandler(
    IRequestReader reader, IServiceScopeFactory scopes, TimeProvider clock, ILogger<SettleHandler> logger)
{
    public async Task<SettleResult> HandleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        int expired = 0, completed = 0, failed = 0;

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

        var result = new SettleResult(expired, completed, failed);
        LogRan(result.Expired, result.Completed, result.Failed);
        return result;
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

    [LoggerMessage(Level = LogLevel.Information, Message = "SettleRan {Expired} {Completed} {Failed}")]
    private partial void LogRan(int expired, int completed, int failed);

    [LoggerMessage(Level = LogLevel.Information, Message = "RequestSettled {Step} {RequestId}")]
    private partial void LogSettled(string step, Guid requestId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SettleConflicted {Step} {RequestId}")]
    private partial void LogConflicted(string step, Guid requestId);

    [LoggerMessage(Level = LogLevel.Error, Message = "SettleFailed {Step} {RequestId}")]
    private partial void LogFailed(Exception exception, string step, Guid requestId);
}
