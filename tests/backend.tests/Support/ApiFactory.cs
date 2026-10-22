using BabysittingPool.Domain.Credits;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace backend.tests.Support;

/// <summary>The real API over HTTP on a throwaway SQLite file and a fake clock.</summary>
public sealed class ApiFactory(string environment = "E2E") : WebApplicationFactory<Program>
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"babysitting-api-{Guid.NewGuid():N}.db");

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Default"] = $"Data Source={_path}" }));
        builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            services.Replace(ServiceDescriptor.Singleton(Clock));
        });
    }

    public HttpClient CreateMemberClient(Guid memberId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Member-Id", memberId.ToString());
        return client;
    }

    public Task<Guid> SeedAccountAsync(Credits available, Credits escrowed, params Credits[] grants) =>
        AccountSeeder.SeedAccountAsync(Services, Clock, available, escrowed, grants);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file)) File.Delete(file);
    }
}
