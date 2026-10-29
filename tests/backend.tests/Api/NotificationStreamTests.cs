using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure;
using backend.tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Api;

/// <summary>Live delivery over Server-Sent Events (ADR 0019), on a real host and real SQLite.</summary>
public class NotificationStreamTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly ApiFactory _on = new();

    public void Dispose()
    {
        _on.Dispose();
    }

    private static async Task<Guid> SeedAsync(ApiFactory factory) =>
        await factory.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0), Credits.FromDecimal(10m));

    private static async Task<JsonElement> PostRequestAsync(ApiFactory factory, HttpClient requester)
    {
        var response = await requester.PostAsJsonAsync("/api/requests", new
        {
            startsAt = factory.Clock.GetUtcNow().AddHours(10),
            durationMinutes = 60,
            childCount = 1,
            location = "RequestingFamilysHouse",
            transport = "None",
            lunch = false,
            snack = false,
            dinner = false,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private int Connections => _on.Services.GetRequiredService<NotificationBroadcaster>().ConnectionCount;

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Patience);
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    /// <summary>An open stream and what it has received so far.</summary>
    private sealed class EventStream(HttpResponseMessage response, System.IO.Stream body) : IDisposable
    {
        private readonly StreamReader reader = new(body);

        public async Task<(string Event, JsonElement Data)> NextEventAsync()
        {
            using var timeout = new CancellationTokenSource(Patience);
            string? name = null;
            while (await reader.ReadLineAsync(timeout.Token) is { } line)
            {
                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    name = line["event: ".Length..];
                }
                else if (line.StartsWith("data: ", StringComparison.Ordinal) && name is not null)
                {
                    return (name, JsonDocument.Parse(line["data: ".Length..]).RootElement.Clone());
                }
            }

            throw new InvalidOperationException("The stream ended.");
        }

        public async Task<string> NextLineAsync()
        {
            using var timeout = new CancellationTokenSource(Patience);
            return (await reader.ReadLineAsync(timeout.Token))!;
        }

        public void Dispose()
        {
            reader.Dispose();
            response.Dispose();
        }
    }

    private async Task<EventStream> OpenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/notifications/stream", HttpCompletionOption.ResponseHeadersRead);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        return new EventStream(response, await response.Content.ReadAsStreamAsync());
    }

    [Fact]
    public async Task A_signed_out_caller_gets_401()
    {
        using var client = _on.CreateSpaClient();

        (await client.GetAsync("/api/notifications/stream")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_post_reaches_the_other_members_only_and_matches_their_list_item()
    {
        var requester = await SeedAsync(_on);
        var other = await SeedAsync(_on);
        using var requesterClient = _on.CreateMemberClient(requester);
        using var otherClient = _on.CreateMemberClient(other);
        using var requesterStream = await OpenAsync(requesterClient);
        using var otherStream = await OpenAsync(otherClient);

        await PostRequestAsync(_on, requesterClient);

        var (name, data) = await otherStream.NextEventAsync();
        name.Should().Be("notification.created");
        var listed = (await otherClient.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("items").EnumerateArray().Single();
        data.GetRawText().Should().Be(listed.GetRawText());
        data.GetProperty("kind").GetString().Should().Be("request_posted");

        // The Requester was not notified: the next thing on their stream is the one sent for the Claim below.
        var sitter = await SeedAsync(_on);
        using var sitterClient = _on.CreateMemberClient(sitter);
        var requestId = data.GetProperty("requestId").GetString();
        (await sitterClient.PostAsync($"/api/requests/{requestId}/claim", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var (claimedName, claimed) = await requesterStream.NextEventAsync();
        claimedName.Should().Be("notification.created");
        claimed.GetProperty("kind").GetString().Should().Be("request_claimed");
        claimed.GetProperty("sitterId").GetString().Should().Be(sitter.ToString());
    }

    [Fact]
    public async Task Dismissing_reaches_the_same_members_other_connections_and_nobody_else()
    {
        var requester = await SeedAsync(_on);
        var other = await SeedAsync(_on);
        var third = await SeedAsync(_on);
        using var requesterClient = _on.CreateMemberClient(requester);
        using var otherClient = _on.CreateMemberClient(other);
        using var otherSecondTab = _on.CreateMemberClient(other);
        using var thirdClient = _on.CreateMemberClient(third);
        using var otherStream = await OpenAsync(otherClient);
        using var otherSecondStream = await OpenAsync(otherSecondTab);
        using var thirdStream = await OpenAsync(thirdClient);
        await PostRequestAsync(_on, requesterClient);
        var id = (await otherStream.NextEventAsync()).Data.GetProperty("id").GetString();
        (await otherSecondStream.NextEventAsync()).Event.Should().Be("notification.created");
        (await thirdStream.NextEventAsync()).Event.Should().Be("notification.created");

        (await otherClient.DeleteAsync($"/api/notifications/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (name, data) = await otherSecondStream.NextEventAsync();
        name.Should().Be("notification.dismissed");
        data.GetProperty("id").GetString().Should().Be(id);
        (await otherStream.NextEventAsync()).Event.Should().Be("notification.dismissed");

        // Clearing is the next event the third Member sees, so the dismissal above never reached them.
        (await thirdClient.DeleteAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await thirdStream.NextEventAsync()).Event.Should().Be("notifications.cleared");
    }

    [Fact]
    public async Task Dismissing_another_members_id_publishes_nothing()
    {
        var requester = await SeedAsync(_on);
        var other = await SeedAsync(_on);
        var third = await SeedAsync(_on);
        using var requesterClient = _on.CreateMemberClient(requester);
        using var otherClient = _on.CreateMemberClient(other);
        using var thirdClient = _on.CreateMemberClient(third);
        using var otherStream = await OpenAsync(otherClient);
        await PostRequestAsync(_on, requesterClient);
        var id = (await otherStream.NextEventAsync()).Data.GetProperty("id").GetString();

        (await thirdClient.DeleteAsync($"/api/notifications/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await otherClient.DeleteAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await otherStream.NextEventAsync()).Event.Should().Be("notifications.cleared");
    }

    [Fact]
    public async Task A_closed_connection_is_cleaned_up()
    {
        using var client = _on.CreateMemberClient(await SeedAsync(_on));
        var stream = await OpenAsync(client);
        Connections.Should().Be(1);

        stream.Dispose();

        await WaitUntilAsync(() => Connections == 0);
    }

    [Fact]
    public async Task An_idle_stream_gets_a_heartbeat_comment_every_30_seconds()
    {
        using var client = _on.CreateMemberClient(await SeedAsync(_on));
        using var stream = await OpenAsync(client);
        (await stream.NextLineAsync()).Should().Be(": connected");

        _on.Clock.Advance(TimeSpan.FromSeconds(30));

        await stream.NextLineAsync(); // the blank line ending the first comment
        (await stream.NextLineAsync()).Should().Be(": heartbeat");
    }

    [Fact]
    public async Task Rate_limiting_does_not_break_a_stream_that_is_already_open()
    {
        var requester = await SeedAsync(_on);
        var other = await SeedAsync(_on);
        using var requesterClient = _on.CreateMemberClient(requester);
        using var otherClient = _on.CreateMemberClient(other);
        using var otherStream = await OpenAsync(otherClient);

        // Exhaust the Member's per-minute budget on ordinary calls.
        HttpResponseMessage last;
        do
        {
            last = await otherClient.GetAsync("/api/notifications");
        }
        while (last.StatusCode != HttpStatusCode.TooManyRequests);

        await PostRequestAsync(_on, requesterClient);
        (await otherStream.NextEventAsync()).Event.Should().Be("notification.created");

        // The stream has a bucket of its own: a reconnect still works while ordinary calls are limited.
        using var reconnected = await OpenAsync(otherClient);
        Connections.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Opening_streams_is_limited_per_member()
    {
        using var client = _on.CreateMemberClient(await SeedAsync(_on));
        var streams = new List<EventStream>();
        try
        {
            for (var i = 0; i < 30; i++)
            {
                streams.Add(await OpenAsync(client));
            }

            var response = await client.GetAsync("/api/notifications/stream");

            response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }
        finally
        {
            streams.ForEach(s => s.Dispose());
        }
    }
}
