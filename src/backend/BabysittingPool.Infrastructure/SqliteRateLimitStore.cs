using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace BabysittingPool.Infrastructure;

/// <summary>The outcome of counting one hit: whether it is within the limit, and when the window reopens.</summary>
public readonly record struct RateLimitDecision(bool Allowed, TimeSpan RetryAfter);

/// <summary>
/// Fixed-window rate-limit counters that survive a restart (ADR 0014). They live in their own SQLite file next to the
/// main database, so a counter write never queues behind a Ledger transaction and the file needs no migration or backup.
/// One atomic upsert per hit. Keys are stored hashed: they embed email addresses and IPs.
/// </summary>
public sealed class SqliteRateLimitStore
{
    private const string Pragmas = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";

    private static readonly TimeSpan PurgeInterval = TimeSpan.FromMinutes(5);

    private readonly string _connectionString;
    private readonly TimeProvider _time;
    private long _nextPurgeMs;

    public SqliteRateLimitStore(string mainConnectionString, TimeProvider time)
    {
        var main = new SqliteConnectionStringBuilder(mainConnectionString);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = main.DataSource + ".rate-limits",
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        _time = time;
        _nextPurgeMs = Now() + (long)PurgeInterval.TotalMilliseconds;

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS RateLimitCounters (Key TEXT NOT NULL PRIMARY KEY, WindowEnd INTEGER NOT NULL, Count INTEGER NOT NULL) WITHOUT ROWID";
        command.ExecuteNonQuery();
    }

    /// <summary>Counts one hit on <paramref name="key"/>; a hit over <paramref name="limit"/> in the window is refused.</summary>
    public async Task<RateLimitDecision> HitAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken)
    {
        var now = Now();
        await using var connection = Open();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RateLimitCounters (Key, WindowEnd, Count) VALUES ($key, $now + $window, 1)
            ON CONFLICT (Key) DO UPDATE SET
                WindowEnd = CASE WHEN WindowEnd <= $now THEN $now + $window ELSE WindowEnd END,
                Count = CASE WHEN WindowEnd <= $now THEN 1 ELSE Count + 1 END
            RETURNING Count, WindowEnd
            """;
        command.Parameters.AddWithValue("$key", Hash(key));
        command.Parameters.AddWithValue("$now", now);
        command.Parameters.AddWithValue("$window", (long)window.TotalMilliseconds);

        long count;
        long windowEnd;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            count = reader.GetInt64(0);
            windowEnd = reader.GetInt64(1);
        }

        await PurgeExpiredAsync(connection, now, cancellationToken);
        return new RateLimitDecision(count <= limit, TimeSpan.FromMilliseconds(Math.Max(0, windowEnd - now)));
    }

    private async Task PurgeExpiredAsync(SqliteConnection connection, long now, CancellationToken cancellationToken)
    {
        var due = Interlocked.Read(ref _nextPurgeMs);
        if (now < due || Interlocked.CompareExchange(ref _nextPurgeMs, now + (long)PurgeInterval.TotalMilliseconds, due) != due)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM RateLimitCounters WHERE WindowEnd <= $now";
        command.Parameters.AddWithValue("$now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private long Now() => _time.GetUtcNow().ToUnixTimeMilliseconds();

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        // nosemgrep: csharp.lang.security.sqli.csharp-sqli.csharp-sqli -- Pragmas is a compile-time constant
        command.CommandText = Pragmas;
        command.ExecuteNonQuery();
        return connection;
    }

    private static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
