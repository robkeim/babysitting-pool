using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using BabysittingPool.Application.Identity;
using BabysittingPool.Infrastructure;
using backend.tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Api;

public sealed class EmailWiringTests
{
    private static readonly Dictionary<string, string> Smtp = new()
    {
        ["Email:Smtp:Address"] = "co.op@mail.example",
        ["Email:Smtp:Password"] = "not-a-real-password",
        // Nothing listens here, so a send that reached SMTP would fail loudly.
        ["Email:Smtp:Host"] = "127.0.0.1",
        ["Email:Smtp:Port"] = "1",
    };

    [Theory]
    [InlineData("E2E")]
    [InlineData("Testing")]
    public async Task Test_environments_never_resolve_the_smtp_sender_even_when_configured(string environment)
    {
        using var factory = new ApiFactory(environment, settings: Smtp);
        using var client = factory.CreateSpaClient();

        factory.Services.GetService<SmtpEmailSender>().Should().BeNull();
        var sender = factory.Services.GetRequiredService<IEmailSender>();
        sender.Should().BeOfType<EmailOutbox>();

        await factory.InviteAsync("new.person@example.com");
        factory.Services.GetRequiredService<EmailOutbox>().Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task Development_without_secrets_seeds_example_test_members_through_the_outbox_only()
    {
        using var factory = new ApiFactory("Development");
        using var client = factory.CreateSpaClient();

        factory.Services.GetService<SmtpEmailSender>().Should().BeNull();
        var outbox = factory.Services.GetRequiredService<EmailOutbox>();
        outbox.Messages.Should().ContainSingle().Which.To.Should().Be("admin@example.test");
        (await client.GetFromJsonAsync<object[]>("/api/dev/outbox?to=admin@example.test")).Should().ContainSingle();
    }

    [Fact]
    public async Task Development_with_secrets_seeds_plus_addresses_and_does_not_send_at_startup()
    {
        using var factory = new ApiFactory("Development", settings: Smtp);
        using var client = factory.CreateSpaClient();

        factory.Services.GetService<SmtpEmailSender>().Should().NotBeNull();
        var outbox = factory.Services.GetRequiredService<EmailOutbox>();
        // Startup would have thrown on the unreachable SMTP host had it tried to send the Admin's link.
        outbox.Messages.Should().ContainSingle().Which.To.Should().Be("co.op+admin@mail.example");

        var response = await client.PostAsJsonAsync("/api/auth/sign-in-links", new { email = "co.op+ryan@mail.example" });

        // The send was attempted (and failed on the unreachable host), and the link is in the outbox regardless.
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        outbox.Messages.Should().HaveCount(2).And.Contain(m => m.To == "co.op+ryan@mail.example");
    }

    [Fact]
    public async Task An_smtp_failure_surfaces_with_the_message_already_in_the_outbox()
    {
        using var factory = new ApiFactory("Development", settings: Smtp);
        var outbox = factory.Services.GetRequiredService<EmailOutbox>();

        var act = () => factory.InviteAsync("new.person@example.com");

        await act.Should().ThrowAsync<Exception>();
        outbox.Messages.Should().Contain(m => m.To == "new.person@example.com");
    }
}
