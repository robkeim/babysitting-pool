using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure.Persistence;
using BabysittingPool.Tests.Support;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Tests.Api;

/// <summary>
/// Ownership and redaction (spec §2A, §4; ADR 0014): a Member reaches only their own data, and the exact address,
/// instructions and notes reach only the Requester and the claiming Sitter.
/// </summary>
public class ResourceOwnershipTests : IDisposable
{
    private const string Address = "1 Secret Lane";
    private const string Instructions = "door code 4711";
    private const string Notes = "the cat bites";

    private static readonly string[] Secrets = [Address, Instructions, Notes];

    private static readonly string[] Browse =
        ["/api/requests/open", "/api/me/requests", "/api/me/claims", "/api/notifications"];

    private readonly ApiFactory _factory = new();
    private readonly List<HttpClient> _clients = [];

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _factory.Dispose();
    }

    private sealed record Person(Guid Id, string Email, HttpClient Client);

    private async Task<Person> SeedPersonAsync(long availableQuarterCredits = 40, bool isAdmin = false)
    {
        var id = await _factory.SeedAccountAsync(Credits.FromQuarterCredits(availableQuarterCredits), Credits.FromQuarterCredits(0));
        if (isAdmin)
        {
            await _factory.MakeAdminAsync(id);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BabysittingPoolDbContext>();
        await db.Members.Where(m => m.Id == id).ExecuteUpdateAsync(m => m
            .SetProperty(x => x.Address, Address)
            .SetProperty(x => x.Instructions, Instructions));
        var email = await db.Members.Where(m => m.Id == id).Select(m => m.Email).SingleAsync();
        var client = _factory.CreateMemberClient(id);
        _clients.Add(client);
        return new Person(id, email, client);
    }

    private async Task<string> PostRequestAsync(Person requester, string? notes = Notes)
    {
        var response = await requester.Client.PostAsJsonAsync("/api/requests", new
        {
            startsAt = _factory.Clock.GetUtcNow().AddHours(10),
            durationMinutes = 120,
            childCount = 1,
            location = "RequestingFamilysHouse",
            transport = "None",
            lunch = false,
            snack = false,
            dinner = false,
            notes,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task<string> BodyAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Everything the person can browse, joined, so one assertion covers every list.</summary>
    private static async Task<string> EverythingVisibleToAsync(Person person) =>
        string.Join('\n', await Task.WhenAll(Browse.Select(path => BodyAsync(person.Client, path))));

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().Should().Be(code);
    }

    private static async Task AssertNoSecretsAsync(Person person, params string[] otherLeaks)
    {
        var text = await EverythingVisibleToAsync(person);
        foreach (var secret in Secrets.Concat(otherLeaks))
        {
            text.Should().NotContain(secret, $"{person.Id} must not see it");
        }
    }

    // ---- Requests ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Other_members_cannot_cancel_edit_the_notes_of_or_withdraw_a_claim_on_a_posted_request()
    {
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var c = await SeedPersonAsync();
        var id = await PostRequestAsync(a);

        foreach (var stranger in new[] { b, c })
        {
            await AssertProblemAsync(await stranger.Client.PostAsync($"/api/requests/{id}/cancel", null), HttpStatusCode.NotFound, "request_not_found");
            await AssertProblemAsync(await stranger.Client.PutAsJsonAsync($"/api/requests/{id}/notes", new { notes = "hijack" }), HttpStatusCode.NotFound, "request_not_found");
            await AssertProblemAsync(await stranger.Client.PostAsync($"/api/requests/{id}/withdraw", null), HttpStatusCode.NotFound, "request_not_found");
        }

        var mine = (await a.Client.GetFromJsonAsync<JsonElement>("/api/me/requests")).GetProperty("items").EnumerateArray().Single();
        mine.GetProperty("status").GetString().Should().Be("Posted");
        mine.GetProperty("notes").GetString().Should().Be(Notes);
    }

    [Fact]
    public async Task On_a_claimed_request_a_stranger_the_sitter_and_the_requester_each_stay_within_their_role()
    {
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var c = await SeedPersonAsync();
        var id = await PostRequestAsync(a);
        (await b.Client.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();

        // C: a stranger. B: the Sitter, who may withdraw but not cancel or edit. A: the Requester, who may not withdraw.
        await AssertProblemAsync(await c.Client.PostAsync($"/api/requests/{id}/cancel", null), HttpStatusCode.NotFound, "request_not_found");
        await AssertProblemAsync(await c.Client.PutAsJsonAsync($"/api/requests/{id}/notes", new { notes = "hijack" }), HttpStatusCode.NotFound, "request_not_found");
        await AssertProblemAsync(await c.Client.PostAsync($"/api/requests/{id}/withdraw", null), HttpStatusCode.NotFound, "request_not_found");
        await AssertProblemAsync(await b.Client.PostAsync($"/api/requests/{id}/cancel", null), HttpStatusCode.NotFound, "request_not_found");
        await AssertProblemAsync(await b.Client.PutAsJsonAsync($"/api/requests/{id}/notes", new { notes = "hijack" }), HttpStatusCode.NotFound, "request_not_found");
        await AssertProblemAsync(await a.Client.PostAsync($"/api/requests/{id}/withdraw", null), HttpStatusCode.NotFound, "request_not_found");

        var mine = (await a.Client.GetFromJsonAsync<JsonElement>("/api/me/requests")).GetProperty("items").EnumerateArray().Single();
        mine.GetProperty("status").GetString().Should().Be("Claimed");
        mine.GetProperty("notes").GetString().Should().Be(Notes);
        (await b.Client.GetFromJsonAsync<JsonElement>("/api/me/claims")).GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task A_strangers_failure_on_another_members_request_matches_the_failure_on_a_random_id()
    {
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var c = await SeedPersonAsync();
        var id = await PostRequestAsync(a);
        (await b.Client.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();
        var random = Guid.NewGuid();

        async Task<(HttpStatusCode, string)> Fingerprint(Func<string, Task<HttpResponseMessage>> send, string target)
        {
            var response = await send(target);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;
            return (response.StatusCode, $"{root.GetProperty("code").GetString()}|{root.GetProperty("title").GetString()}|{detail}");
        }

        var calls = new Dictionary<string, Func<string, Task<HttpResponseMessage>>>
        {
            ["cancel"] = t => c.Client.PostAsync($"/api/requests/{t}/cancel", null),
            ["withdraw"] = t => c.Client.PostAsync($"/api/requests/{t}/withdraw", null),
            ["notes"] = t => c.Client.PutAsJsonAsync($"/api/requests/{t}/notes", new { notes = "x" }),
        };
        foreach (var (name, send) in calls)
        {
            var real = await Fingerprint(send, id);
            var unknown = await Fingerprint(send, random.ToString());
            real.Should().Be(unknown, $"{name} must not reveal that the Request exists");
            real.Item1.Should().Be(HttpStatusCode.NotFound);
        }
    }

    // ---- Redaction ---------------------------------------------------------------------------------------

    [Fact]
    public async Task While_posted_the_address_instructions_and_notes_reach_no_other_member()
    {
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var c = await SeedPersonAsync();
        await PostRequestAsync(a);

        await AssertNoSecretsAsync(b, a.Email);
        await AssertNoSecretsAsync(c, a.Email);
    }

    [Fact]
    public async Task Once_claimed_only_the_requester_and_the_claiming_sitter_see_them_and_not_a_third_member()
    {
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var c = await SeedPersonAsync();
        var id = await PostRequestAsync(a);
        (await b.Client.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();

        var requesterSees = await BodyAsync(a.Client, "/api/me/requests");
        var sitterSees = await BodyAsync(b.Client, "/api/me/claims");
        foreach (var secret in Secrets)
        {
            requesterSees.Should().Contain(secret);
            sitterSees.Should().Contain(secret);
        }

        await AssertNoSecretsAsync(c, a.Email, b.Email);
        // Notifications never carry them, to anyone.
        foreach (var person in new[] { a, b, c })
        {
            var notifications = await BodyAsync(person.Client, "/api/notifications");
            foreach (var secret in Secrets)
            {
                notifications.Should().NotContain(secret);
            }
        }
    }

    [Fact]
    public async Task After_the_claim_is_withdrawn_the_sitter_and_everyone_else_lose_sight_of_them()
    {
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var c = await SeedPersonAsync();
        var id = await PostRequestAsync(a);
        (await b.Client.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();
        (await b.Client.PostAsync($"/api/requests/{id}/withdraw", null)).EnsureSuccessStatusCode();

        await AssertNoSecretsAsync(b);
        await AssertNoSecretsAsync(c);
    }

    [Fact]
    public async Task After_the_requester_cancels_a_claimed_request_the_former_sitter_and_everyone_else_lose_sight_of_them()
    {
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var c = await SeedPersonAsync();
        var id = await PostRequestAsync(a);
        (await b.Client.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();
        (await a.Client.PostAsync($"/api/requests/{id}/cancel", null)).EnsureSuccessStatusCode();

        await AssertNoSecretsAsync(b);
        await AssertNoSecretsAsync(c);
    }

    [Fact]
    public async Task After_settle_the_sitter_keeps_the_notes_and_a_third_member_still_sees_nothing()
    {
        var admin = await SeedPersonAsync(isAdmin: true);
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var c = await SeedPersonAsync();
        var id = await PostRequestAsync(a);
        (await b.Client.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();
        _factory.Clock.Advance(TimeSpan.FromHours(11));
        (await admin.Client.PostAsync("/api/admin/settle", null)).EnsureSuccessStatusCode();

        var claim = (await b.Client.GetFromJsonAsync<JsonElement>("/api/me/claims")).GetProperty("items").EnumerateArray().Single();
        claim.GetProperty("status").GetString().Should().Be("Completed");
        claim.GetProperty("notes").GetString().Should().Be(Notes);
        await AssertNoSecretsAsync(c, a.Email, b.Email);
    }

    // ---- Notifications -----------------------------------------------------------------------------------

    [Fact]
    public async Task A_member_cannot_list_or_delete_another_members_notifications()
    {
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var id = await PostRequestAsync(a);
        (await b.Client.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();
        var aNotifications = (await a.Client.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("items");
        aNotifications.GetArrayLength().Should().BeGreaterThan(0, "A is told about the claim");
        var notificationId = aNotifications[0].GetProperty("id").GetString();

        var bList = await BodyAsync(b.Client, "/api/notifications");
        var foreign = await b.Client.DeleteAsync($"/api/notifications/{notificationId}");

        bList.Should().NotContain(notificationId!);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        // ADR 0019: a foreign id is 404, while an id already gone (or never issued) is an idempotent 204.
        (await a.Client.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("items").GetArrayLength()
            .Should().Be(aNotifications.GetArrayLength(), "B's delete removed nothing of A's");
    }

    // ---- Profile and account -----------------------------------------------------------------------------

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/me/account")]
    [InlineData("/api/me/profile")]
    [InlineData("/api/me/requests")]
    [InlineData("/api/me/claims")]
    public async Task Me_routes_ignore_a_member_id_and_answer_with_the_callers_own_data(string path)
    {
        var a = await SeedPersonAsync(availableQuarterCredits: 40);
        var b = await SeedPersonAsync(availableQuarterCredits: 8);
        await PostRequestAsync(a);

        var plain = await BodyAsync(b.Client, path);
        foreach (var parameter in new[] { "memberId", "member", "id", "requesterId" })
        {
            var spoofed = await BodyAsync(b.Client, $"{path}?{parameter}={a.Id}");
            spoofed.Should().Be(plain, $"?{parameter}= must not change whose data {path} returns");
        }

        plain.Should().NotContain(a.Id.ToString());
        plain.Should().NotContain(a.Email);
    }

    [Fact]
    public async Task The_profile_and_account_belong_to_the_caller()
    {
        var a = await SeedPersonAsync(availableQuarterCredits: 40);
        var b = await SeedPersonAsync(availableQuarterCredits: 8);

        (await a.Client.GetFromJsonAsync<JsonElement>("/api/me/account")).GetProperty("availableQuarterCredits").GetInt64().Should().Be(40);
        (await b.Client.GetFromJsonAsync<JsonElement>($"/api/me/account?memberId={a.Id}")).GetProperty("availableQuarterCredits").GetInt64().Should().Be(8);
        (await b.Client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("memberId").GetGuid().Should().Be(b.Id);
    }

    // ---- Admin routes ------------------------------------------------------------------------------------

    [Fact]
    public async Task Every_admin_route_answers_403_to_a_member()
    {
        var member = await SeedPersonAsync();
        var routes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/admin", StringComparison.Ordinal))
            .ToArray();
        routes.Should().NotBeEmpty();

        var checkedRoutes = new List<string>();
        foreach (var route in routes)
        {
            foreach (var method in route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                var path = route.RoutePattern.PathSegments.Aggregate("", (acc, segment) => acc + "/" + string.Concat(
                    segment.Parts.Select(part => part switch
                    {
                        Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart literal => literal.Content,
                        _ => Guid.NewGuid().ToString(),
                    })));
                using var request = new HttpRequestMessage(new HttpMethod(method), path);
                if (method is "POST" or "PUT")
                {
                    request.Content = JsonContent.Create(new { });
                }

                var response = await member.Client.SendAsync(request);

                response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route.RoutePattern.RawText}");
                checkedRoutes.Add($"{method} {route.RoutePattern.RawText}");
            }
        }

        checkedRoutes.Should().Contain(["POST /api/admin/settle", "POST /api/admin/members/{id:guid}/offboard", "POST /api/admin/members/{id:guid}/erase"]);
    }

    [Fact]
    public async Task A_forbidden_admin_action_changes_nothing()
    {
        var member = await SeedPersonAsync();
        var victim = await SeedPersonAsync();

        (await member.Client.PostAsync($"/api/admin/members/{victim.Id}/offboard", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await victim.Client.GetAsync("/api/me/account")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Offboarded and erased Members -------------------------------------------------------------------

    [Fact]
    public async Task An_old_session_can_neither_read_nor_act_after_offboarding_or_erasure()
    {
        var admin = await SeedPersonAsync(isAdmin: true);
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var id = await PostRequestAsync(a);
        var other = await PostRequestAsync(b);
        (await admin.Client.PostAsync($"/api/admin/members/{a.Id}/offboard", null)).EnsureSuccessStatusCode();

        async Task AssertLockedOutAsync(Person person)
        {
            foreach (var path in new[] { "/api/me", "/api/me/account", "/api/me/profile", "/api/requests/open", "/api/me/requests", "/api/me/claims", "/api/notifications" })
            {
                (await person.Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, path);
            }

            (await person.Client.PostAsync($"/api/requests/{other}/claim", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await person.Client.PostAsync($"/api/requests/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await person.Client.PutAsJsonAsync($"/api/requests/{id}/notes", new { notes = "late" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await person.Client.DeleteAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        await AssertLockedOutAsync(a);
        (await admin.Client.PostAsync($"/api/admin/members/{a.Id}/erase", null)).EnsureSuccessStatusCode();
        await AssertLockedOutAsync(a);
        (await b.Client.GetAsync("/api/me/account")).StatusCode.Should().Be(HttpStatusCode.OK, "other sessions are untouched");
    }

    [Fact]
    public async Task Erasure_removes_the_requesters_notes_and_address_from_every_view()
    {
        var admin = await SeedPersonAsync(isAdmin: true);
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var id = await PostRequestAsync(a);
        (await b.Client.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();
        _factory.Clock.Advance(TimeSpan.FromHours(11));
        (await admin.Client.PostAsync("/api/admin/settle", null)).EnsureSuccessStatusCode();
        (await admin.Client.PostAsync($"/api/admin/members/{a.Id}/offboard", null)).EnsureSuccessStatusCode();
        (await admin.Client.PostAsync($"/api/admin/members/{a.Id}/erase", null)).EnsureSuccessStatusCode();

        var claim = (await b.Client.GetFromJsonAsync<JsonElement>("/api/me/claims")).GetProperty("items").EnumerateArray().Single();
        claim.GetProperty("notes").ValueKind.Should().Be(JsonValueKind.Null);
        claim.GetProperty("address").ValueKind.Should().Be(JsonValueKind.Null);
        (await BodyAsync(admin.Client, "/api/admin/members")).Should().NotContain(a.Email);
    }

    // ---- Admin views -------------------------------------------------------------------------------------

    [Fact]
    public async Task Admin_views_never_carry_the_address_instructions_notes_or_child_data()
    {
        var admin = await SeedPersonAsync(isAdmin: true);
        var a = await SeedPersonAsync();
        var b = await SeedPersonAsync();
        var id = await PostRequestAsync(a);
        (await b.Client.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();
        await PostRequestAsync(a);

        var views = new List<string>();
        foreach (var path in new[]
        {
            "/api/admin/members",
            $"/api/admin/members/{a.Id}",
            $"/api/admin/members/{b.Id}",
            $"/api/admin/members/{a.Id}/requests",
            $"/api/admin/members/{b.Id}/requests",
            "/api/admin/pool-overview",
            "/api/admin/dormant-members",
            "/api/admin/invitations",
        })
        {
            views.Add(await BodyAsync(admin.Client, path));
        }

        foreach (var view in views)
        {
            foreach (var secret in Secrets)
            {
                view.Should().NotContain(secret);
            }

            using var json = JsonDocument.Parse(view);
            var names = PropertyNames(json.RootElement).ToList();
            names.Should().NotContain(
                ["address", "instructions", "notes", "postcode", "commune", "inseeCode", "children"],
                "an Admin view carries no personal request data beyond the rough area (ADR 0014)");
        }

        // The requests view keeps to counts, area and cost: no email either.
        views[3].Should().NotContain(a.Email).And.NotContain(b.Email);
    }

    private static IEnumerable<string> PropertyNames(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;
                    foreach (var nested in PropertyNames(property.Value))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in PropertyNames(item))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }
}
