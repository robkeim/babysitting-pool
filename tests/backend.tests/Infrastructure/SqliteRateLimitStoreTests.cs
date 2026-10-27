using AwesomeAssertions;
using BabysittingPool.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace backend.tests.Infrastructure;

/// <summary>Rate-limit counters (ADR 0014) outlive the process that wrote them.</summary>
public class SqliteRateLimitStoreTests : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"babysitting-limits-{Guid.NewGuid():N}.db");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    private SqliteRateLimitStore NewStore() => new($"Data Source={_path}", _clock);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(_path) + "*"))
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Hits_over_the_limit_are_refused_until_the_window_reopens()
    {
        var store = NewStore();

        for (var i = 0; i < 3; i++)
        {
            (await store.HitAsync("k", 3, Window, default)).Allowed.Should().BeTrue();
        }

        var refused = await store.HitAsync("k", 3, Window, default);
        refused.Allowed.Should().BeFalse();
        refused.RetryAfter.Should().Be(Window);

        _clock.Advance(Window);
        (await store.HitAsync("k", 3, Window, default)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Keys_are_counted_separately()
    {
        var store = NewStore();
        await store.HitAsync("a", 1, Window, default);

        (await store.HitAsync("a", 1, Window, default)).Allowed.Should().BeFalse();
        (await store.HitAsync("b", 1, Window, default)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Counters_survive_a_restart()
    {
        var before = NewStore();
        await before.HitAsync("k", 1, Window, default);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var after = NewStore();
        var decision = await after.HitAsync("k", 1, Window, default);

        decision.Allowed.Should().BeFalse();
        decision.RetryAfter.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Keys_are_not_stored_in_clear()
    {
        await NewStore().HitAsync("member@example.com", 1, Window, default);
        SqliteConnection.ClearAllPools();

        System.Text.Encoding.Latin1.GetString(await File.ReadAllBytesAsync(_path + ".rate-limits")).Should().NotContain("member@example.com");
    }
}
