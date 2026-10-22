using System.Buffers.Text;
using System.Globalization;
using System.Text;

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
        var after = query.Cursor is null ? null : Decode(query.Cursor);

        // Ask for one extra entry to learn whether another page follows.
        var account = await reader.FindAsync(query.MemberId, after, limit + 1, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var items = account.LedgerEntries.Take(limit).ToList();
        var nextCursor = account.LedgerEntries.Count > limit ? Encode(items[^1]) : null;
        return new AccountView(
            account.AvailableQuarterCredits,
            account.EscrowedQuarterCredits,
            new Page<LedgerEntryView>(items, nextCursor));
    }

    private static string Encode(LedgerEntryView last) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            $"{last.OccurredAt.Ticks.ToString(CultureInfo.InvariantCulture)}|{last.Id:N}"));

    private static LedgerCursor Decode(string cursor)
    {
        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)).Split('|');
            return new LedgerCursor(
                new DateTime(long.Parse(parts[0], CultureInfo.InvariantCulture), DateTimeKind.Utc),
                Guid.ParseExact(parts[1], "N"));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new InvalidCursorException(ex);
        }
    }
}
