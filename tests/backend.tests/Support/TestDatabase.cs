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

    public Task<Guid> SeedAccountAsync(Credits available, Credits escrowed, params Credits[] grants) =>
        AccountSeeder.SeedAccountAsync(Services, Clock, available, escrowed, grants);

    public Task<Guid> SeedSimultaneousGrantsAsync(int count) =>
        AccountSeeder.SeedSimultaneousGrantsAsync(Services, Clock, count);

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file)) File.Delete(file);
    }
}
