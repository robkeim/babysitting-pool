using System.Net;
using AwesomeAssertions;
using BabysittingPool.Tests.Support;
using Microsoft.Data.Sqlite;

namespace BabysittingPool.Tests.Api;

/// <summary>Fly keeps a Machine only while <c>/health</c> is 200, so it must notice a broken database (ADR 0018).</summary>
public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Answers_200_when_the_database_answers()
    {
        using var factory = new PlainApiFactory("Production", "admin@coop.example");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("""{"status":"ok"}""");
    }

    [Fact]
    public async Task Answers_503_without_details_when_the_database_is_unreachable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"babysitting-health-{Guid.NewGuid():N}.db");
        using var factory = new PlainApiFactory("Production", "admin@coop.example", settings: new() { ["ConnectionStrings:Default"] = $"Data Source={path}" });
        using var client = factory.CreateClient();
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        SqliteConnection.ClearAllPools();
        await File.WriteAllTextAsync(path, "this is not a SQLite database, just text padded out to look like a broken volume");
        File.Delete(path + "-wal");
        File.Delete(path + "-shm");

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Be("""{"status":"unavailable"}""");
        File.Delete(path);
    }
}
