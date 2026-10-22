namespace BabysittingPool.Application.Credits;

public sealed record GetMyAccountQuery(Guid MemberId, string? Cursor = null, int? Limit = null);

/// <summary>The supplied paging cursor was not one this API issued.</summary>
public sealed class InvalidCursorException(Exception inner) : Exception("Invalid cursor.", inner);

public sealed class GetMyAccountHandler(ICreditAccountReader reader)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    /// <summary>Returns the Member's Credit Account and Ledger, or null when they have none.</summary>
    public async Task<AccountView?> HandleAsync(GetMyAccountQuery query, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);
        var after = query.Cursor is null ? null : ToLedgerCursor(TimestampCursor.Decode(query.Cursor));

        // Ask for one extra entry to learn whether another page follows.
        var account = await reader.FindAsync(query.MemberId, after, limit + 1, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var items = account.LedgerEntries.Take(limit).ToList();
        var nextCursor = account.LedgerEntries.Count > limit ? TimestampCursor.Encode(items[^1].OccurredAt, items[^1].Id) : null;
        return new AccountView(
            account.AvailableQuarterCredits,
            account.EscrowedQuarterCredits,
            new Page<LedgerEntryView>(items, nextCursor));
    }

    private static LedgerCursor ToLedgerCursor((DateTime At, Guid Id) position) => new(position.At, position.Id);
}
