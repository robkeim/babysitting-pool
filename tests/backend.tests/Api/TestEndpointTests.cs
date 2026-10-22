using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using backend.tests.Support;

namespace backend.tests.Api;

public class TestEndpointTests
{
    [Fact]
    public async Task Seeds_a_member_whose_account_the_api_then_serves_in_E2E()
    {
        using var factory = new ApiFactory("E2E");
        using var client = factory.CreateClient();

        var seeded = await client.PostAsJsonAsync("/api/test/members", new { grantsQuarterCredits = new[] { 40, 1 } });

        seeded.StatusCode.Should().Be(HttpStatusCode.Created);
        var memberId = (await seeded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("memberId").GetGuid();
        using var member = factory.CreateMemberClient(memberId);
        var account = await member.GetFromJsonAsync<JsonElement>("/api/me/account");
        account.GetProperty("availableQuarterCredits").GetInt64().Should().Be(41);
        account.GetProperty("ledger").GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Sets_the_server_clock_in_E2E()
    {
        using var factory = new ApiFactory("E2E");
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/test/clock", new { now = "2027-01-02T03:04:05Z" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        factory.Clock.GetUtcNow().Should().Be(new DateTimeOffset(2027, 1, 2, 3, 4, 5, TimeSpan.Zero));
    }

    [Fact]
    public async Task Rejects_an_empty_or_negative_seed_with_400()
    {
        using var factory = new ApiFactory("E2E");
        using var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/test/members", new { grantsQuarterCredits = Array.Empty<int>() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/test/members", new { grantsQuarterCredits = new[] { -1 } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task Does_not_exist_outside_E2E(string environment)
    {
        using var factory = new ApiFactory(environment, bootstrapAdminEmail: "root@example.com");
        using var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/test/members", new { grantsQuarterCredits = new[] { 40 } }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync("/api/test/clock", new { now = "2027-01-02T03:04:05Z" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
