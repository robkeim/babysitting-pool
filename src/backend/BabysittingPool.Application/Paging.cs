using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace BabysittingPool.Application;

/// <summary>A list response: <c>{ items, nextCursor }</c> (ADR 0006).</summary>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);

/// <summary>The supplied paging cursor was not one this API issued.</summary>
public sealed class InvalidCursorException(Exception inner) : Exception("Invalid cursor.", inner);

/// <summary>A row's place in a list ordered by a timestamp, ties broken by id.</summary>
public readonly record struct KeysetPosition(DateTime At, Guid Id);

/// <summary>
/// One page asked for: the limit policy, the decoded cursor and the over-fetch, in one place. Readers apply
/// <see cref="After"/> and fetch <see cref="Take"/> rows; <see cref="ToPage{TRow, TItem}"/> turns those rows into
/// the response page and its next cursor.
/// </summary>
public sealed record PageRequest
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    private PageRequest(int limit, KeysetPosition? after)
    {
        Limit = limit;
        After = after;
    }

    public int Limit { get; }

    /// <summary>The position the page starts after, or null for the first page.</summary>
    public KeysetPosition? After { get; }

    /// <summary>Rows to fetch: one more than the page holds, to learn whether another page follows.</summary>
    public int Take => Limit + 1;

    /// <summary>Throws <see cref="InvalidCursorException"/> when <paramref name="cursor"/> is not one we issued.</summary>
    public static PageRequest From(string? cursor, int? limit) =>
        new(Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit), cursor is null ? null : Decode(cursor));

    public Page<TItem> ToPage<TRow, TItem>(
        IReadOnlyList<TRow> rows, Func<TRow, KeysetPosition> position, Func<TRow, TItem> project)
    {
        var items = rows.Take(Limit).ToList();
        var next = rows.Count > Limit ? Encode(position(items[^1])) : null;
        return new Page<TItem>(items.Select(project).ToList(), next);
    }

    private static string Encode(KeysetPosition position) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            $"{position.At.Ticks.ToString(CultureInfo.InvariantCulture)}|{position.Id:N}"));

    private static KeysetPosition Decode(string cursor)
    {
        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)).Split('|');
            return new KeysetPosition(
                new DateTime(long.Parse(parts[0], CultureInfo.InvariantCulture), DateTimeKind.Utc),
                Guid.ParseExact(parts[1], "N"));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new InvalidCursorException(ex);
        }
    }
}
