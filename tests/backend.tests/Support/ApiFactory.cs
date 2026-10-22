using AwesomeAssertions;
using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
using BabysittingPool.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace backend.tests.Support;

/// <summary>The real API over HTTP on a throwaway SQLite file and a fake clock.</summary>
internal sealed class ApiFactory(string environment = "E2E", string? bootstrapAdminEmail = null) : WebApplicationFactory<Program>
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"babysitting-api-{Guid.NewGuid():N}.db");

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        // UseSetting, not ConfigureAppConfiguration: Program reads these while building, before the latter applies.
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_path}");
        builder.UseSetting("App:BaseUrl", "https://coop.example");
        if (bootstrapAdminEmail is not null)
        {
            builder.UseSetting("Bootstrap:AdminEmail", bootstrapAdminEmail);
        }

        builder.ConfigureServices(services =>
        {
            if (environment == "Production")
            {
                // Production has no email provider yet; tests stand in the outbox so the API can start.
                services.RemoveAll<IEmailSender>();
                services.AddEmailOutbox();
            }

            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            services.Replace(ServiceDescriptor.Singleton(Clock));
        });
    }

    /// <summary>A client that is signed in as the Member, carrying the CSRF header like the SPA does.</summary>
    public HttpClient CreateMemberClient(Guid memberId)
    {
        var secret = Secrets.Generate();
        using (var scope = Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IIdentityStore>()
                .AddSessionAsync(Session.Start(memberId, Secrets.Hash(secret), Clock.GetUtcNow().UtcDateTime), CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        var client = CreateSpaClient();
        client.DefaultRequestHeaders.Add("Cookie", $"session={secret}");
        return client;
    }

    /// <summary>A client without cookies that sends the CSRF header like the SPA does.</summary>
    public HttpClient CreateSpaClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "fetch");
        return client;
    }

    public async Task InviteAsync(string email, bool isAdmin = false)
    {
        await using var scope = Services.CreateAsyncScope();
        EmailAddress.TryParse(email, out var address).Should().BeTrue();
        await scope.ServiceProvider.GetRequiredService<IssueInvitationHandler>()
            .HandleAsync(new IssueInvitationCommand(address, isAdmin), CancellationToken.None);
    }

    public Task<Guid> SeedMemberAsync() => AccountSeeder.SeedMemberAsync(Services, Clock);

    public Task<Guid> SeedAccountAsync(Credits available, Credits escrowed, params Credits[] grants) =>
        AccountSeeder.SeedAccountAsync(Services, Clock, available, escrowed, grants);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
