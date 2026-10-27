using System.Security.Cryptography;
using System.Text;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

/// <summary>The outcome of counting one hit: whether it is within the limit, and when the window reopens.</summary>
public readonly record struct RateLimitDecision(bool Allowed, TimeSpan RetryAfter);

/// <summary>
/// Fixed-window rate-limit counters that survive a restart (ADR 0014). Keys are stored hashed: they embed email
/// addresses and IPs.
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

        // Two or three atomic statements rather than one raw upsert (ADR 0004): between them another hit can
        // land, which only over-counts, so the error is toward refusing, never toward letting a flood through.
        var counter = db.Counters.Where(c => c.Key == hash);
        for (var attempt = 0; ; attempt++)
        {
            var updated = await counter.ExecuteUpdateAsync(set => set
                .SetProperty(c => c.Count, c => c.WindowEnd <= now ? 1 : c.Count + 1)
                .SetProperty(c => c.WindowEnd, c => c.WindowEnd <= now ? now + windowMs : c.WindowEnd), cancellationToken);
            if (updated > 0)
            {
                break;
            }

            db.Counters.Add(new RateLimitCounterRecord { Key = hash, WindowEnd = now + windowMs, Count = 1 });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // A concurrent first hit created the row: count against it instead.
                db.ChangeTracker.Clear();
            }
        }

        var hit = await counter.AsNoTracking().Select(c => new Hit(c.Count, c.WindowEnd)).SingleAsync(cancellationToken);

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
