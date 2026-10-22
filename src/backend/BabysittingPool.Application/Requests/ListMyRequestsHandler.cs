using BabysittingPool.Application.Credits;

namespace BabysittingPool.Application.Requests;

public sealed record ListMyRequestsQuery(Guid MemberId, string? Cursor = null, int? Limit = null);

/// <summary>The Requester's "My Requests" list with each Request's state (spec §4).</summary>
public sealed class ListMyRequestsHandler(IRequestReader reader, TimeProvider clock)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    public async Task<Page<RequestView>> HandleAsync(ListMyRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);
        var after = query.Cursor is null ? ((DateTime, Guid)?)null : TimestampCursor.Decode(query.Cursor);

        // Ask for one extra row to learn whether another page follows.
        var rows = await reader.ListByRequesterAsync(query.MemberId, after, limit + 1, cancellationToken);
        var page = rows.Take(limit).ToList();
        var nextCursor = rows.Count > limit ? TimestampCursor.Encode(page[^1].PostedAt, page[^1].Id) : null;

        var now = clock.GetUtcNow().UtcDateTime;
        return new Page<RequestView>(page.Select(r => r.ToView(now)).ToList(), nextCursor);
    }
}
