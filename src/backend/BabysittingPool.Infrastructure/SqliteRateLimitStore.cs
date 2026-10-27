using System.Security.Cryptography;
using System.Text;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

/// <summary>The outcome of counting one hit: whether it is within the limit, and when the window reopens.</summary>
public readonly record struct RateLimitDecision(bool Allowed, TimeSpan RetryAfter);

/// <summary>
/// Fixed-window rate-limit counters that survive a restart (ADR 0014). One atomic upsert per hit: LINQ cannot express
/// it, and a read-then-update would race under concurrency.
/// </summary>
public sealed class SqliteRateLimitStore(IDbContextFactory<RateLimitDbContext> contexts, TimeProvider time)
{
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromMinutes(5);

    private long _nextPurgeMs = time.GetUtcNow().ToUnixTimeMilliseconds() + (long)PurgeInterval.TotalMilliseconds;

    /// <summary>Counts one hit on <paramref name="key"/>; a hit over <paramref name="limit"/> in the window is refused.</summary>
    public async Task<RateLimitDecision> HitAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        var hash = Hash(key);
        var windowMs = (long)window.TotalMilliseconds;
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var rows = await db.Database.SqlQuery<Hit>($"""
            INSERT INTO RateLimitCounters (Key, WindowEnd, Count) VALUES ({hash}, {now + windowMs}, 1)
            ON CONFLICT (Key) DO UPDATE SET
                WindowEnd = CASE WHEN WindowEnd <= {now} THEN {now + windowMs} ELSE WindowEnd END,
                Count = CASE WHEN WindowEnd <= {now} THEN 1 ELSE Count + 1 END
            RETURNING Count, WindowEnd
            """).ToListAsync(cancellationToken);
        var hit = rows.Single();

        await PurgeExpiredAsync(db, now, cancellationToken);
        return new RateLimitDecision(hit.Count <= limit, TimeSpan.FromMilliseconds(Math.Max(0, hit.WindowEnd - now)));
    }

    private async Task PurgeExpiredAsync(RateLimitDbContext db, long now, CancellationToken cancellationToken)
    {
        var due = Interlocked.Read(ref _nextPurgeMs);
        if (now < due || Interlocked.CompareExchange(ref _nextPurgeMs, now + (long)PurgeInterval.TotalMilliseconds, due) != due)
        {
            return;
        }

        await db.Counters.Where(c => c.WindowEnd <= now).ExecuteDeleteAsync(cancellationToken);
    }

    private static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private sealed record Hit(long Count, long WindowEnd);
}
