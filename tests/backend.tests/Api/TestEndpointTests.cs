using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Application.Flags;
using backend.tests.Support;

namespace backend.tests.Api;

public class TestEndpointTests
{
    [Fact]
    public async Task Seeds_a_member_whose_account_the_api_then_serves_in_E2E()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSpaClient();

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
        using var factory = new ApiFactory();
        using var client = factory.CreateSpaClient();

        var response = await client.PutAsJsonAsync("/api/test/clock", new { now = "2027-01-02T03:04:05Z" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        factory.Clock.GetUtcNow().Should().Be(new DateTimeOffset(2027, 1, 2, 3, 4, 5, TimeSpan.Zero));
    }

    [Fact]
    public async Task Rejects_an_empty_or_negative_seed_with_400()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateSpaClient();

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
        using var factory = new PlainApiFactory(environment, bootstrapAdminEmail: "root@example.com");
        using var client = factory.CreateSpaClient();

        (await client.PostAsJsonAsync("/api/test/members", new { grantsQuarterCredits = new[] { 40 } }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync("/api/test/clock", new { now = "2027-01-02T03:04:05Z" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<HttpResponseMessage> PostFlagOverrideAsync(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/test/feature-flags")
        {
            Content = JsonContent.Create(new { name = "alpha", enabled = true }),
        };
        if (token is not null)
        {
            request.Headers.Add("X-E2E-Token", token);
        }

        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public async Task The_flag_override_is_404_without_the_right_token(string? token)
    {
        using var factory = new ApiFactory(flags: [new FeatureFlag("alpha", false, 1)], e2eToken: "s3cret", sendE2EToken: false);
        using var client = factory.CreateSpaClient();

        (await PostFlagOverrideAsync(client, token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/test/gated/alpha")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_flag_override_is_404_when_no_token_is_configured()
    {
        using var factory = new ApiFactory(flags: [new FeatureFlag("alpha", false, 1)], e2eToken: null);
        using var client = factory.CreateSpaClient();

        (await PostFlagOverrideAsync(client, "anything")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_flag_override_changes_the_flag_with_the_right_token()
    {
        using var factory = new ApiFactory(flags: [new FeatureFlag("alpha", false, 1)], e2eToken: "s3cret");
        using var client = factory.CreateSpaClient();

        var response = await PostFlagOverrideAsync(client, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/test/gated/alpha")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_flag_override_does_not_exist_in_Production()
    {
        using var factory = new PlainApiFactory("Production", bootstrapAdminEmail: "root@example.com");
        using var client = factory.CreateSpaClient();

        (await PostFlagOverrideAsync(client, null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Production_refuses_to_start_with_an_E2E_token()
    {
        using var factory = new PlainApiFactory("Production", bootstrapAdminEmail: "root@example.com", e2eToken: "s3cret");

        var start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage("*E2E:Token*");
    }

    [Theory]
    [InlineData("POST", "/api/test/members")]
    [InlineData("POST", "/api/test/sessions")]
    [InlineData("POST", "/api/test/settle")]
    [InlineData("PUT", "/api/test/clock")]
    [InlineData("GET", "/api/test/outbox")]
    public async Task Every_test_endpoint_is_404_without_the_token(string method, string path)
    {
        using var factory = new ApiFactory(sendE2EToken: false);
        using var client = factory.CreateSpaClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { grantsQuarterCredits = new[] { 40 }, memberId = Guid.NewGuid(), now = "2027-01-02T03:04:05Z" }),
        };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void The_API_assembly_does_not_contain_or_reference_the_test_support()
    {
        var api = typeof(BabysittingPool.Api.ApiHost).Assembly;

        api.GetReferencedAssemblies().Select(a => a.Name).Should().NotContain(
            ["BabysittingPool.TestSupport", "BabysittingPool.E2E", "Microsoft.Extensions.Time.Testing"]);
    }
}
