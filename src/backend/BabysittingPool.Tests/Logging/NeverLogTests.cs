using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using BabysittingPool.Api;
using BabysittingPool.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace BabysittingPool.Tests.Logging;

/// <summary>The never-log list (ADR 0010): the layers that keep tokens, addresses, names and free text out of the log.</summary>
public partial class NeverLogTests : IDisposable
{
    private const string Email = "leak.probe@example.com";
    private const string SitterEmail = "sitter.probe@example.com";
    private const string DisplayName = "Zelda Probeworth";
    private const string Note = "Bring the PURPLE-NOTE-9431 blanket";
    private const string EditedNote = "Actually the ORANGE-NOTE-7720 one";

    private readonly CapturedLogEvents _logs = new();
    private readonly ApiFactory _factory;

    public NeverLogTests() =>
        _factory = new ApiFactory(configureServices: services => services.AddSingleton<ILogEventSink>(_logs));

    public void Dispose() => _factory.Dispose();

    [GeneratedRegex(@"\{@")]
    private static partial Regex Destructuring();

    [Fact]
    public void No_message_template_destructures_an_object()
    {
        var offenders = BackendSources()
            .Where(file => Destructuring().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(BackendRoot(), file));

        offenders.Should().BeEmpty("only scalar ids and values are logged (ADR 0010)");
    }

    [Fact]
    public async Task The_main_flows_log_no_token_email_display_name_note_or_session_secret()
    {
        var secrets = new List<string> { Email, SitterEmail, DisplayName, "Sam Sitterson", Note, EditedNote };

        // Invitation to account, sign out, sign back in by link, a rejected link and a rejected invitation.
        var (owner, ownerSession) = await JoinAsync(Email, DisplayName, secrets);
        var (sitter, _) = await JoinAsync(SitterEmail, "Sam Sitterson", secrets);
        secrets.Add(ownerSession);
        (await owner.DeleteAsync("/api/auth/sessions/current")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.PostAsJsonAsync("/api/auth/sign-in-links", new { email = Email })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var signInToken = TokenIn(await LatestLinkAsync(Email));
        secrets.Add(signInToken);
        (await owner.PostAsJsonAsync("/api/auth/sessions", new { token = signInToken })).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync("/api/auth/sessions", new { token = signInToken })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await owner.PostAsJsonAsync("/api/auth/sign-in-links", new { email = "ghost.probe@example.com" })).StatusCode.Should().Be(HttpStatusCode.Accepted);
        secrets.Add("ghost.probe@example.com");

        // Post, claim, edit the note, withdraw, cancel; plus a failing call and a lost claim.
        var posted = await owner.PostAsJsonAsync("/api/requests", new
        {
            startsAt = _factory.Clock.GetUtcNow().AddHours(10),
            durationMinutes = 120,
            childCount = 1,
            location = "SittersHouse",
            transport = "None",
            notes = Note,
        });
        posted.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await posted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        (await sitter.PostAsync($"/api/requests/{id}/claim", null)).EnsureSuccessStatusCode();
        (await sitter.PostAsync($"/api/requests/{id}/claim", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.PutAsJsonAsync($"/api/requests/{id}/notes", new { notes = EditedNote })).EnsureSuccessStatusCode();
        (await sitter.PostAsync($"/api/requests/{id}/withdraw", null)).EnsureSuccessStatusCode();
        (await owner.PostAsync($"/api/requests/{id}/cancel", null)).EnsureSuccessStatusCode();
        (await owner.GetAsync("/api/me/requests")).EnsureSuccessStatusCode();

        _factory.Clock.Advance(TimeSpan.FromDays(1));
        var logged = _logs.AllText();
        _logs.Events.Should().Contain(e => e.MessageTemplate.Text.StartsWith("HttpRequestHandled", StringComparison.Ordinal));
        foreach (var secret in secrets)
        {
            logged.Should().NotContain(secret);
        }
    }

    [Fact]
    public async Task Rejected_client_logs_leave_none_of_their_content_in_the_log()
    {
        var secrets = new List<string> { "CLIENT-LOG-LEAK-1", "CLIENT-LOG-LEAK-2", "CLIENT-LOG-LEAK-3" };
        using var client = _factory.CreateMemberClient(await _factory.SeedMemberAsync());

        var unknownField = await client.PostAsJsonAsync("/api/client-logs", new { message = "m", code = "c", route = "/", appVersion = "1", extra = secrets[0] });
        var badRoute = await client.PostAsJsonAsync("/api/client-logs", new { message = "m", code = "c", route = $"/?{secrets[1]}", appVersion = "1" });
        var oversized = await client.PostAsJsonAsync("/api/client-logs", new { message = secrets[2] + new string('x', 5000), code = "c", route = "/", appVersion = "1" });

        unknownField.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badRoute.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        oversized.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        var logged = _logs.AllText();
        logged.Should().Contain("ClientLogRejected");
        foreach (var secret in secrets)
        {
            logged.Should().NotContain(secret);
        }
    }

    [Fact]
    public void The_masking_backstop_hides_a_denylisted_property_whatever_its_value()
    {
        var sink = new CapturedLogEvents();
        using var logger = new Serilog.LoggerConfiguration().Enrich.With<DenylistMaskingEnricher>().WriteTo.Sink(sink).CreateLogger();

        logger.Information("Done {MagicLinkToken} {RequesterEmail} {Notes} {Authorization} {Cookie} {Password} {RequestId}", "tok", "a@b.c", "free text", "Bearer x", "session=x", "pw", 7);

        var logEvent = sink.Events.Single();
        foreach (var name in new[] { "MagicLinkToken", "RequesterEmail", "Notes", "Authorization", "Cookie", "Password" })
        {
            CapturedLogEvents.Text(logEvent, name).Should().Be("***", name);
        }

        CapturedLogEvents.Text(logEvent, "RequestId").Should().Be("7");
        sink.AllText().Should().NotContain("tok").And.NotContain("a@b.c").And.NotContain("free text");
    }

    [Fact]
    public void The_masking_backstop_reaches_into_destructured_objects_dictionaries_and_lists()
    {
        var sink = new CapturedLogEvents();
        using var logger = new Serilog.LoggerConfiguration().Enrich.With<DenylistMaskingEnricher>().WriteTo.Sink(sink).CreateLogger();

        logger.Information("Done {@Member} {@Bag} {@Many}", new { Id = 1, Email = "deep@example.com" }, new Dictionary<string, string> { ["Token"] = "deep-token", ["Kind"] = "ok" }, new[] { new { Note = "deep note" } });

        sink.AllText().Should().NotContain("deep@example.com").And.NotContain("deep-token").And.NotContain("deep note").And.Contain("ok");
    }

    [Fact]
    public void A_property_that_merely_contains_a_denylisted_word_is_not_masked()
    {
        DenylistMaskingEnricher.IsDenied("NotificationId").Should().BeFalse();
        DenylistMaskingEnricher.IsDenied("RequestId").Should().BeFalse();
        DenylistMaskingEnricher.IsDenied("emailaddress").Should().BeTrue();
    }

    private async Task<(HttpClient Client, string SessionSecret)> JoinAsync(string email, string displayName, List<string> secrets)
    {
        await _factory.InviteAsync(email);
        var token = TokenIn(await LatestLinkAsync(email));
        secrets.Add(token);
        var client = _factory.CreateClient(new() { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "fetch");
        var response = await client.PostAsJsonAsync("/api/invitations/accept", new { token, displayName });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("session=", StringComparison.Ordinal));
        var secret = cookie["session=".Length..cookie.IndexOf(';', StringComparison.Ordinal)];
        secrets.Add(secret);
        return (client, secret);
    }

    private async Task<string> LatestLinkAsync(string to)
    {
        using var client = _factory.CreateClient();
        var outbox = await client.GetFromJsonAsync<JsonElement>($"/api/test/outbox?to={Uri.EscapeDataString(to)}");
        return outbox.EnumerateArray().Last().GetProperty("link").GetString()!;
    }

    private static string TokenIn(string link) => System.Web.HttpUtility.ParseQueryString(new Uri(link).Query)["token"]!;

    private static string BackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BabysittingPool.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("BabysittingPool.sln not found above the test output.");
    }

    private static IEnumerable<string> BackendSources() =>
        Directory.EnumerateFiles(BackendRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin" or "BabysittingPool.Tests"));
}
