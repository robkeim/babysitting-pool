using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using backend.tests.Support;

namespace backend.tests.Api;

public class NotificationEndpointTests : IDisposable
{
    private readonly ApiFactory _on = new(features: new Dictionary<string, string> { ["notifications"] = "true" });
    private readonly ApiFactory _off = new();

    public void Dispose()
    {
        _on.Dispose();
        _off.Dispose();
    }

    private static async Task<Guid> SeedAsync(ApiFactory factory) =>
        await factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0), Credits.FromDecimal(10m));

    private static async Task PostRequestAsync(ApiFactory factory, HttpClient requester) =>
        (await requester.PostAsJsonAsync("/api/requests", new
        {
            startsAt = factory.Clock.GetUtcNow().AddHours(10),
            durationMinutes = 60,
            childCount = 1,
            location = "RequestingFamilysHouse",
            transport = "None",
            lunch = false,
            snack = false,
            dinner = false,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

    [Fact]
    public async Task While_the_flag_is_off_every_endpoint_answers_404_feature_off()
    {
        using var client = _off.CreateMemberClient(await SeedAsync(_off));

        foreach (var response in new[]
        {
            await client.GetAsync("/api/notifications"),
            await client.DeleteAsync($"/api/notifications/{Guid.NewGuid()}"),
            await client.DeleteAsync("/api/notifications"),
        })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("feature_off");
        }
    }

    [Fact]
    public async Task Signed_out_callers_get_401_even_while_the_flag_is_off()
    {
        using var client = _off.CreateSpaClient();

        (await client.GetAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_post_shows_in_the_other_members_list_with_ids_kind_and_payload_only()
    {
        var requester = await SeedAsync(_on);
        var other = await SeedAsync(_on);
        using var requesterClient = _on.CreateMemberClient(requester);
        using var otherClient = _on.CreateMemberClient(other);
        await PostRequestAsync(_on, requesterClient);

        var list = await otherClient.GetFromJsonAsync<JsonElement>("/api/notifications");

        var item = list.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("kind").GetString().Should().Be("request_posted");
        item.GetProperty("area").GetString().Should().Be("Riverside");
        item.GetProperty("startsAt").GetString().Should().Be("2026-03-01T19:00:00Z");
        item.GetRawText().Should().NotContain("River Rd");
        list.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        (await requesterClient.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("items").GetArrayLength().Should().Be(0, "a Requester is not told about their own Request");
    }

    [Fact]
    public async Task Dismiss_is_idempotent_and_another_members_id_is_404()
    {
        var requester = await SeedAsync(_on);
        var other = await SeedAsync(_on);
        var third = await SeedAsync(_on);
        using var requesterClient = _on.CreateMemberClient(requester);
        using var otherClient = _on.CreateMemberClient(other);
        using var thirdClient = _on.CreateMemberClient(third);
        await PostRequestAsync(_on, requesterClient);
        var id = (await otherClient.GetFromJsonAsync<JsonElement>("/api/notifications"))
            .GetProperty("items")[0].GetProperty("id").GetString();

        (await thirdClient.DeleteAsync($"/api/notifications/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherClient.DeleteAsync($"/api/notifications/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await otherClient.DeleteAsync($"/api/notifications/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await otherClient.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("items").GetArrayLength().Should().Be(0);
        (await thirdClient.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Dismiss_all_clears_the_callers_list_only_and_a_bad_cursor_is_400()
    {
        var requester = await SeedAsync(_on);
        var other = await SeedAsync(_on);
        using var requesterClient = _on.CreateMemberClient(requester);
        using var otherClient = _on.CreateMemberClient(other);
        await PostRequestAsync(_on, requesterClient);
        await PostRequestAsync(_on, otherClient);

        (await otherClient.DeleteAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await otherClient.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("items").GetArrayLength().Should().Be(0);
        (await requesterClient.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("items").GetArrayLength().Should().Be(1);
        (await otherClient.GetAsync("/api/notifications?cursor=nonsense")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
