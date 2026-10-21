using BabysittingPool.Application;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace backend.tests.Support;

/// <summary>
/// A throwaway SQLite file built from the real migrations (ADR 0004), with a fake clock.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"babysitting-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;

    public TestDatabase()
    {
        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddApplication();
        services.AddInfrastructure($"Data Source={_path}");
        _services = services.BuildServiceProvider();
    }

    public FakeTimeProvider Clock { get; }

    public IServiceProvider Services => _services;

    public static async Task<TestDatabase> CreateMigratedAsync()
    {
        var database = new TestDatabase();
        await database.Services.MigrateDatabaseAsync();
        return database;
    }

    public AsyncServiceScope CreateScope() => _services.CreateAsyncScope();

    public BabysittingPoolDbContext NewContext(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<BabysittingPoolDbContext>();

    /// <summary>Arranges a Credit Account whose Ledger holds the given Grants, one hour apart.</summary>
    public async Task<Guid> SeedAccountAsync(Credits available, Credits escrowed, params Credits[] grants)
    {
        var memberId = Guid.NewGuid();
        await using var scope = CreateScope();
        var db = NewContext(scope);
        db.CreditAccounts.Add(new CreditAccountRecord { MemberId = memberId, Available = available, Escrowed = escrowed });
        for (var i = 0; i < grants.Length; i++)
        {
            db.LedgerEntries.Add(new LedgerEntryRecord
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                Kind = LedgerEntryKind.Grant,
                Amount = grants[i],
                OccurredAt = Clock.GetUtcNow().UtcDateTime.AddHours(i),
            });
        }
        await db.SaveChangesAsync();
        return memberId;
    }

    /// <summary>Arranges an account whose Ledger holds <paramref name="count"/> Grants at the very same instant.</summary>
    public async Task<Guid> SeedSimultaneousGrantsAsync(int count)
    {
        var memberId = Guid.NewGuid();
        await using var scope = CreateScope();
        var db = NewContext(scope);
        db.CreditAccounts.Add(new CreditAccountRecord
        {
            MemberId = memberId,
            Available = Credits.FromQuarterCredits(count),
            Escrowed = Credits.FromQuarterCredits(0),
        });
        for (var i = 0; i < count; i++)
        {
            db.LedgerEntries.Add(new LedgerEntryRecord
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                Kind = LedgerEntryKind.Grant,
                Amount = Credits.FromQuarterCredits(1),
                OccurredAt = Clock.GetUtcNow().UtcDateTime,
            });
        }
        await db.SaveChangesAsync();
        return memberId;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file)) File.Delete(file);
    }
}
