using BabysittingPool.Application.Credits;

namespace BabysittingPool.Application.Requests;

public sealed record ListMyClaimsQuery(Guid MemberId, string? Cursor = null, int? Limit = null);

/// <summary>
/// The Sitter's "My Claims" list, latest start first. The exact address is included: the Sitter holds the claim
/// (spec §4.6). A Request the Sitter withdrew from has no Sitter any more, so it drops off the list.
/// </summary>
public sealed class ListMyClaimsHandler(IRequestReader reader, TimeProvider clock)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    public async Task<Page<RequestView>> HandleAsync(ListMyClaimsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);
        var after = query.Cursor is null ? ((DateTime, Guid)?)null : TimestampCursor.Decode(query.Cursor);

        var rows = await reader.ListClaimedByAsync(query.MemberId, after, limit + 1, cancellationToken);
        var page = rows.Take(limit).ToList();
        var nextCursor = rows.Count > limit ? TimestampCursor.Encode(page[^1].StartsAt, page[^1].Id) : null;

        var now = clock.GetUtcNow().UtcDateTime;
        return new Page<RequestView>(page.Select(r => r.ToView(now)).ToList(), nextCursor);
    }
}
