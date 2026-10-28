using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Infrastructure;
using backend.tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Api;

public class AuthEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static string TokenIn(string link) => System.Web.HttpUtility.ParseQueryString(new Uri(link).Query)["token"]!;

    private async Task<string> LatestLinkAsync(string to)
    {
        using var client = _factory.CreateClient();
        var outbox = await client.GetFromJsonAsync<JsonElement>($"/api/dev/outbox?to={Uri.EscapeDataString(to)}");
        return outbox.EnumerateArray().Last().GetProperty("link").GetString()!;
    }

    private async Task<HttpClient> JoinAsync(string email)
    {
        await _factory.InviteAsync(email);
        var token = TokenIn(await LatestLinkAsync(email));
        var client = _factory.CreateClient(new() { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "fetch");
        var response = await client.PostAsJsonAsync("/api/invitations/accept", new { token, displayName = "Ada" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return client;
    }

    [Fact]
    public async Task An_invited_person_creates_an_account_and_is_signed_in_by_cookie()
    {
        using var client = await JoinAsync("ada@example.com");

        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        me.GetProperty("email").GetString().Should().Be("ada@example.com");
        me.GetProperty("displayName").GetString().Should().Be("Ada");
        me.GetProperty("isAdmin").GetBoolean().Should().BeFalse();
        var account = await client.GetFromJsonAsync<JsonElement>("/api/me/account");
        account.GetProperty("availableQuarterCredits").GetInt64().Should().Be(40);
    }

    [Fact]
    public async Task A_member_changes_their_language_and_it_is_stored_on_the_member()
    {
        using var client = await JoinAsync("ada@example.com");
        (await client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("language").GetString().Should().Be("en-US");

        var changed = await client.PutAsJsonAsync("/api/me/language", new { language = "en-XA" });
        var rejected = await client.PutAsJsonAsync("/api/me/language", new { language = "de-DE" });

        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertProblemAsync(rejected, HttpStatusCode.BadRequest, "invalid_request");
        (await client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("language").GetString().Should().Be("en-XA");
    }

    [Fact]
    public async Task Unicode_test_is_not_a_choosable_language_in_production()
    {
        using var production = new ApiFactory("Production", bootstrapAdminEmail: "root@example.com");
        var memberId = await production.SeedMemberAsync();
        using var client = production.CreateMemberClient(memberId);

        var response = await client.PutAsJsonAsync("/api/me/language", new { language = "en-XA" });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_request");
        (await client.PutAsJsonAsync("/api/me/language", new { language = "en-US" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task The_session_cookie_is_http_only_and_same_site_lax()
    {
        await _factory.InviteAsync("ada@example.com");
        using var client = _factory.CreateSpaClient();

        var response = await client.PostAsJsonAsync(
            "/api/invitations/accept", new { token = TokenIn(await LatestLinkAsync("ada@example.com")), displayName = "Ada" });

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("session=", StringComparison.Ordinal));
        cookie.Should().Contain("httponly").And.Contain("samesite=lax");
    }

    [Fact]
    public async Task Previewing_an_invitation_returns_the_invited_address_and_rejects_unknown_tokens()
    {
        await _factory.InviteAsync("ada@example.com");
        using var client = _factory.CreateSpaClient();

        var ok = await client.PostAsJsonAsync("/api/invitations/preview", new { token = TokenIn(await LatestLinkAsync("ada@example.com")) });
        var bad = await client.PostAsJsonAsync("/api/invitations/preview", new { token = "nope" });

        (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("email").GetString().Should().Be("ada@example.com");
        await AssertProblemAsync(bad, HttpStatusCode.UnprocessableEntity, "invitation_invalid");
    }

    [Fact]
    public async Task Accepting_with_an_invalid_display_name_is_a_400_that_does_not_use_the_invitation()
    {
        await _factory.InviteAsync("ada@example.com");
        var token = TokenIn(await LatestLinkAsync("ada@example.com"));
        using var client = _factory.CreateSpaClient();

        var rejected = await client.PostAsJsonAsync("/api/invitations/accept", new { token, displayName = "  " });
        var accepted = await client.PostAsJsonAsync("/api/invitations/accept", new { token, displayName = "Ada" });

        await AssertProblemAsync(rejected, HttpStatusCode.BadRequest, "invalid_request");
        accepted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_member_signs_in_with_an_emailed_link_and_signs_out()
    {
        (await JoinAsync("ada@example.com")).Dispose();
        using var client = _factory.CreateClient(new() { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "fetch");

        var requested = await client.PostAsJsonAsync("/api/auth/sign-in-links", new { email = "Ada@Example.com" });
        var link = await LatestLinkAsync("ada@example.com");
        var started = await client.PostAsJsonAsync("/api/auth/sessions", new { token = TokenIn(link) });
        var again = await client.PostAsJsonAsync("/api/auth/sessions", new { token = TokenIn(link) });

        requested.StatusCode.Should().Be(HttpStatusCode.Accepted);
        started.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertProblemAsync(again, HttpStatusCode.UnprocessableEntity, "sign_in_link_invalid");
        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.DeleteAsync("/api/auth/sessions/current")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertProblemAsync(await client.GetAsync("/api/me"), HttpStatusCode.Unauthorized, "unauthenticated");
    }

    [Fact]
    public async Task Requesting_a_link_answers_202_for_unknown_addresses_and_400_for_malformed_ones()
    {
        using var client = _factory.CreateSpaClient();

        (await client.PostAsJsonAsync("/api/auth/sign-in-links", new { email = "ghost@example.com" }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        await AssertProblemAsync(
            await client.PostAsJsonAsync("/api/auth/sign-in-links", new { email = "nope" }), HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task Me_is_401_without_a_session()
    {
        using var client = _factory.CreateClient();

        await AssertProblemAsync(await client.GetAsync("/api/me"), HttpStatusCode.Unauthorized, "unauthenticated");
    }

    [Theory]
    [InlineData("POST", "/api/auth/sign-in-links")]
    [InlineData("POST", "/api/auth/sessions")]
    [InlineData("POST", "/api/invitations/accept")]
    [InlineData("DELETE", "/api/auth/sessions/current")]
    [InlineData("PUT", "/api/me/language")]
    public async Task State_changing_calls_without_the_csrf_header_are_403(string method, string path)
    {
        using var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { email = "ada@example.com", token = "x", displayName = "Ada" }),
        };
        var response = await client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "csrf_header_missing");
    }

    [Fact]
    public async Task Development_seeds_an_admin_and_three_members_and_logs_the_admin_link()
    {
        using var development = new ApiFactory("Development");
        using var client = development.CreateClient();

        var outbox = await client.GetFromJsonAsync<JsonElement>("/api/dev/outbox?to=admin@example.test");

        outbox.GetArrayLength().Should().Be(1);
        var link = outbox[0].GetProperty("link").GetString()!;
        link.Should().StartWith("https://coop.example/sign-in/verify?token=");

        using var spa = development.CreateSpaClient();
        var members = new Dictionary<string, long>();
        foreach (var email in new[] { "ada@example.test", "ben@example.test", "cleo@example.test" })
        {
            await spa.PostAsJsonAsync("/api/auth/sign-in-links", new { email });
            var token = TokenIn((await client.GetFromJsonAsync<JsonElement>($"/api/dev/outbox?to={email}"))[0].GetProperty("link").GetString()!);
            using var session = development.CreateClient(new() { HandleCookies = true });
            session.DefaultRequestHeaders.Add("X-Requested-With", "fetch");
            (await session.PostAsJsonAsync("/api/auth/sessions", new { token })).StatusCode.Should().Be(HttpStatusCode.NoContent);
            var account = await session.GetFromJsonAsync<JsonElement>("/api/me/account");
            members[email] = account.GetProperty("availableQuarterCredits").GetInt64();
        }

        members.Should().Equal(new Dictionary<string, long>
        {
            ["ada@example.test"] = 40,
            ["ben@example.test"] = 0,
            ["cleo@example.test"] = 102,
        });
    }

    [Fact]
    public async Task The_outbox_does_not_exist_outside_development_and_e2e()
    {
        using var factory = new ApiFactory("Staging");
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/dev/outbox")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Production_refuses_to_start_with_no_admin_and_no_bootstrap_address()
    {
        using var factory = new ApiFactory("Production");

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Bootstrap:AdminEmail*");
    }

    [Fact]
    public void Production_refuses_to_start_without_SMTP_settings()
    {
        using var factory = new ApiFactory("Production", bootstrapAdminEmail: "root@example.com", smtpConfigured: false);

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Email:Smtp*");
    }

    [Fact]
    public async Task Production_invites_the_bootstrap_admin_at_startup()
    {
        using var factory = new ApiFactory("Production", bootstrapAdminEmail: "root@example.com");
        using var client = factory.CreateClient();

        var outbox = factory.Services.GetRequiredService<EmailOutbox>();

        await Task.CompletedTask;
        outbox.Messages.Should().ContainSingle().Which.To.Should().Be("root@example.com");
        outbox.Messages[0].Link.ToString().Should().StartWith("https://coop.example/invitation?token=");
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().Should().Be(code);
    }
}
