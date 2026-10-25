using AwesomeAssertions;
using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Identity;
using backend.tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Application;

public class AdminInvitationHandlerTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static EmailAddress Email(string value)
    {
        EmailAddress.TryParse(value, out var email).Should().BeTrue();
        return email;
    }

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _database.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Task<InviteMemberResult> InviteAsync(string email) =>
        RunAsync(sp => sp.GetRequiredService<InviteMemberHandler>()
            .HandleAsync(new InviteMemberCommand(Email(email)), CancellationToken.None));

    private Task<ResendInvitationResult> ResendAsync(Guid id) =>
        RunAsync(sp => sp.GetRequiredService<ResendInvitationHandler>()
            .HandleAsync(new ResendInvitationCommand(id), CancellationToken.None));

    private Task<IReadOnlyList<InvitationView>> ListAsync() =>
        RunAsync(sp => sp.GetRequiredService<ListInvitationsHandler>().HandleAsync(CancellationToken.None));

    private Task<SignedIn?> AcceptAsync(string token) =>
        RunAsync(sp => sp.GetRequiredService<AcceptInvitationHandler>().HandleAsync(
            new AcceptInvitationCommand(token, DisplayNameOf("Ada")), CancellationToken.None));

    private static DisplayName DisplayNameOf(string value)
    {
        DisplayName.TryCreate(value, out var name).Should().BeTrue();
        return name;
    }

    private string LastTokenFor(string email) =>
        System.Web.HttpUtility.ParseQueryString(_database.Outbox.Messages.Last(m => m.To == email).Link.Query)["token"]!;

    [Fact]
    public async Task Inviting_emails_a_link_and_lists_the_invitation_as_pending()
    {
        var result = await InviteAsync("ada@example.com");

        result.Outcome.Should().Be(InviteMemberOutcome.Invited);
        result.Invitation!.Status.Should().Be(InvitationStatus.Pending);
        _database.Outbox.Messages.Should().ContainSingle(m => m.To == "ada@example.com");
        (await ListAsync()).Should().ContainSingle().Which.Email.Should().Be("ada@example.com");
    }

    [Fact]
    public async Task Inviting_an_address_that_already_has_an_unaccepted_invitation_is_refused()
    {
        await InviteAsync("ada@example.com");

        var again = await InviteAsync("ada@example.com");

        again.Outcome.Should().Be(InviteMemberOutcome.AlreadyInvited);
        (await ListAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task Inviting_a_current_member_is_refused()
    {
        await InviteAsync("ada@example.com");
        (await AcceptAsync(LastTokenFor("ada@example.com"))).Should().NotBeNull();

        var again = await InviteAsync("ada@example.com");

        again.Outcome.Should().Be(InviteMemberOutcome.AlreadyAMember);
    }

    [Fact]
    public async Task The_list_shows_pending_accepted_and_expired_newest_first()
    {
        await InviteAsync("old@example.com");
        _database.Clock.Advance(TimeSpan.FromDays(8));
        await InviteAsync("accepted@example.com");
        (await AcceptAsync(LastTokenFor("accepted@example.com"))).Should().NotBeNull();
        _database.Clock.Advance(TimeSpan.FromHours(1));
        await InviteAsync("new@example.com");

        var list = await ListAsync();

        list.Select(i => (i.Email, i.Status)).Should().Equal(
            ("new@example.com", InvitationStatus.Pending),
            ("accepted@example.com", InvitationStatus.Accepted),
            ("old@example.com", InvitationStatus.Expired));
        list[1].AcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Resending_an_expired_invitation_makes_it_usable_again_and_kills_the_old_link()
    {
        await InviteAsync("ada@example.com");
        var oldToken = LastTokenFor("ada@example.com");
        _database.Clock.Advance(TimeSpan.FromDays(8));
        var id = (await ListAsync()).Single().Id;

        var result = await ResendAsync(id);

        result.Outcome.Should().Be(ResendInvitationOutcome.Sent);
        result.Invitation!.Status.Should().Be(InvitationStatus.Pending);
        result.Invitation.Id.Should().Be(id);
        (await ListAsync()).Should().ContainSingle();
        (await AcceptAsync(oldToken)).Should().BeNull();
        (await AcceptAsync(LastTokenFor("ada@example.com"))).Should().NotBeNull();
    }

    [Fact]
    public async Task Resending_a_pending_invitation_extends_it_and_replaces_the_link()
    {
        await InviteAsync("ada@example.com");
        var oldToken = LastTokenFor("ada@example.com");
        var id = (await ListAsync()).Single().Id;
        _database.Clock.Advance(TimeSpan.FromDays(3));

        var result = await ResendAsync(id);

        result.Invitation!.ExpiresAt.Should().Be(_database.Clock.GetUtcNow().UtcDateTime.AddDays(7));
        LastTokenFor("ada@example.com").Should().NotBe(oldToken);
        (await AcceptAsync(oldToken)).Should().BeNull();
    }

    [Fact]
    public async Task Resending_an_accepted_invitation_is_refused()
    {
        await InviteAsync("ada@example.com");
        (await AcceptAsync(LastTokenFor("ada@example.com"))).Should().NotBeNull();

        var result = await ResendAsync((await ListAsync()).Single().Id);

        result.Outcome.Should().Be(ResendInvitationOutcome.AlreadyAccepted);
    }

    [Fact]
    public async Task Resending_an_unknown_invitation_is_not_found()
    {
        (await ResendAsync(Guid.NewGuid())).Outcome.Should().Be(ResendInvitationOutcome.NotFound);
    }
}
