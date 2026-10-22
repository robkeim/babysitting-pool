using BabysittingPool.Application.Credits;

namespace BabysittingPool.Application.Requests;

public sealed record ListOpenRequestsQuery(Guid MemberId, string? Cursor = null, int? Limit = null);

/// <summary>
/// "Open Requests": Posted Requests before their Claim Cutoff, earliest start first, rough area only, the
/// Member's own excluded (spec §4.4). The cutoff is checked against the clock, so a Request that is overdue but
/// not yet expired by Settle is already gone (spec §6).
/// </summary>
public sealed class ListOpenRequestsHandler(IRequestReader reader, TimeProvider clock)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    public async Task<Page<OpenRequestView>> HandleAsync(ListOpenRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);
        var after = query.Cursor is null ? ((DateTime, Guid)?)null : TimestampCursor.Decode(query.Cursor);
        var now = clock.GetUtcNow().UtcDateTime;

        // Ask for one extra row to learn whether another page follows.
        var rows = await reader.ListOpenAsync(query.MemberId, now, after, limit + 1, cancellationToken);
        var page = rows.Take(limit).ToList();
        var nextCursor = rows.Count > limit ? TimestampCursor.Encode(page[^1].StartsAt, page[^1].Id) : null;

        return new Page<OpenRequestView>(page.Select(r => r.ToOpenView()).ToList(), nextCursor);
    }
}
