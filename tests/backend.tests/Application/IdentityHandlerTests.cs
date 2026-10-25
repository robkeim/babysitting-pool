using AwesomeAssertions;
using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
using backend.tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Application;

public class IdentityHandlerTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static EmailAddress Email(string value)
    {
        EmailAddress.TryParse(value, out var email).Should().BeTrue();
        return email;
    }

    private static DisplayName Name(string value)
    {
        DisplayName.TryCreate(value, out var name).Should().BeTrue();
        return name;
    }

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _database.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private static string TokenIn(Uri link) => System.Web.HttpUtility.ParseQueryString(link.Query)["token"]!;

    private Task<IssueInvitationResult> InviteAsync(string email, bool isAdmin = false) =>
        RunAsync(sp => sp.GetRequiredService<IssueInvitationHandler>()
            .HandleAsync(new IssueInvitationCommand(Email(email), isAdmin), CancellationToken.None));

    private Task<SignedIn?> AcceptAsync(string token, string name = "Ada") =>
        RunAsync(sp => sp.GetRequiredService<AcceptInvitationHandler>()
            .HandleAsync(new AcceptInvitationCommand(token, Name(name)), CancellationToken.None));

    private Task<MemberView?> ResolveAsync(string secret) =>
        RunAsync(sp => sp.GetRequiredService<ResolveSessionHandler>().HandleAsync(secret, CancellationToken.None));

    private async Task<string> InviteAndGetTokenAsync(string email, bool isAdmin = false)
    {
        (await InviteAsync(email, isAdmin)).Should().Be(IssueInvitationResult.Issued);
        return TokenIn(_database.Outbox.Messages.Last(m => m.To == email).Link);
    }

    [Fact]
    public async Task Accepting_an_invitation_creates_a_member_with_the_starting_balance_and_a_session()
    {
        var token = await InviteAndGetTokenAsync("ada@example.com");

        var signedIn = await AcceptAsync(token);

        signedIn.Should().NotBeNull();
        var me = await ResolveAsync(signedIn!.SessionSecret);
        me.Should().NotBeNull();
        me!.Email.Should().Be("ada@example.com");
        me.DisplayName.Should().Be("Ada");
        me.IsAdmin.Should().BeFalse();
        var account = await RunAsync(sp => sp.GetRequiredService<GetMyAccountHandler>()
            .HandleAsync(new GetMyAccountQuery(me.MemberId), CancellationToken.None));
        account!.AvailableQuarterCredits.Should().Be(40);
        account.Ledger.Items.Should().ContainSingle().Which.Kind.Should().Be(LedgerEntryKind.Grant);
    }

    [Fact]
    public async Task An_invitation_works_once()
    {
        var token = await InviteAndGetTokenAsync("ada@example.com");
        (await AcceptAsync(token)).Should().NotBeNull();

        (await AcceptAsync(token)).Should().BeNull();
    }

    [Fact]
    public async Task Two_simultaneous_acceptances_of_one_invitation_create_one_member()
    {
        var token = await InviteAndGetTokenAsync("ada@example.com");

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AcceptAsync(token)));

        results.Count(r => r is not null).Should().Be(1);
    }

    [Fact]
    public async Task An_invitation_expires_after_its_lifetime()
    {
        var token = await InviteAndGetTokenAsync("ada@example.com");
        _database.Clock.Advance(TimeSpan.FromDays(7));

        (await AcceptAsync(token)).Should().BeNull();
        (await RunAsync(sp => sp.GetRequiredService<PreviewInvitationHandler>().HandleAsync(token, CancellationToken.None)))
            .Should().BeNull();
    }

    [Fact]
    public async Task An_unknown_token_is_not_an_invitation()
    {
        (await AcceptAsync("nope")).Should().BeNull();
    }

    [Fact]
    public async Task Previewing_an_invitation_names_the_invited_address()
    {
        var token = await InviteAndGetTokenAsync("ada@example.com");

        (await RunAsync(sp => sp.GetRequiredService<PreviewInvitationHandler>().HandleAsync(token, CancellationToken.None)))
            .Should().Be("ada@example.com");
    }

    [Fact]
    public async Task Inviting_an_existing_member_is_refused()
    {
        await AcceptAsync(await InviteAndGetTokenAsync("ada@example.com"));

        (await InviteAsync("ADA@example.com")).Should().Be(IssueInvitationResult.AlreadyAMember);
    }

    [Fact]
    public async Task There_is_exactly_one_admin()
    {
        var token = await InviteAndGetTokenAsync("root@example.com", isAdmin: true);
        var second = await InviteAndGetTokenAsync("other@example.com", isAdmin: true);
        var signedIn = await AcceptAsync(token, "Root");
        (await ResolveAsync(signedIn!.SessionSecret))!.IsAdmin.Should().BeTrue();

        (await InviteAsync("third@example.com", isAdmin: true)).Should().Be(IssueInvitationResult.AdminAlreadyExists);
        (await AcceptAsync(second)).Should().BeNull();
    }

    [Fact]
    public async Task A_member_signs_in_with_a_link_that_works_once_and_expires()
    {
        await AcceptAsync(await InviteAndGetTokenAsync("ada@example.com"));
        var address = Email("ada@example.com");

        async Task<string> RequestLinkAsync()
        {
            await RunAsync(async sp =>
            {
                await sp.GetRequiredService<RequestSignInLinkHandler>().HandleAsync(address, CancellationToken.None);
                return 0;
            });
            return TokenIn(_database.Outbox.Messages.Last(m => m.Subject.Contains("sign-in", StringComparison.OrdinalIgnoreCase)).Link);
        }

        Task<SignedIn?> RedeemAsync(string t) =>
            RunAsync(sp => sp.GetRequiredService<RedeemSignInLinkHandler>().HandleAsync(t, CancellationToken.None));

        var first = await RequestLinkAsync();
        var signedIn = await RedeemAsync(first);
        signedIn.Should().NotBeNull();
        (await ResolveAsync(signedIn!.SessionSecret))!.Email.Should().Be("ada@example.com");
        (await RedeemAsync(first)).Should().BeNull();

        var stale = await RequestLinkAsync();
        _database.Clock.Advance(TimeSpan.FromDays(1));
        (await RedeemAsync(stale)).Should().BeNull();
    }

    [Fact]
    public async Task Requesting_a_link_for_an_unknown_address_sends_nothing()
    {
        await RunAsync(async sp =>
        {
            await sp.GetRequiredService<RequestSignInLinkHandler>().HandleAsync(Email("ghost@example.com"), CancellationToken.None);
            return 0;
        });

        _database.Outbox.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task A_session_slides_while_used_and_ends_after_thirty_idle_days()
    {
        var signedIn = (await AcceptAsync(await InviteAndGetTokenAsync("ada@example.com")))!;

        for (var i = 0; i < 3; i++)
        {
            _database.Clock.Advance(TimeSpan.FromDays(20));
            (await ResolveAsync(signedIn.SessionSecret)).Should().NotBeNull();
        }

        _database.Clock.Advance(TimeSpan.FromDays(30));
        (await ResolveAsync(signedIn.SessionSecret)).Should().BeNull();
    }

    [Fact]
    public async Task Ending_a_session_revokes_it()
    {
        var signedIn = (await AcceptAsync(await InviteAndGetTokenAsync("ada@example.com")))!;

        await RunAsync(async sp =>
        {
            await sp.GetRequiredService<EndSessionHandler>().HandleAsync(signedIn.SessionSecret, CancellationToken.None);
            return 0;
        });

        (await ResolveAsync(signedIn.SessionSecret)).Should().BeNull();
    }

    [Fact]
    public async Task Missing_or_unknown_session_secrets_resolve_to_nobody()
    {
        (await RunAsync(sp => sp.GetRequiredService<ResolveSessionHandler>().HandleAsync(null, CancellationToken.None))).Should().BeNull();
        (await ResolveAsync("unknown")).Should().BeNull();
    }

    [Fact]
    public async Task Bootstrap_invites_the_configured_admin_once_while_no_admin_exists()
    {
        Task BootstrapAsync(EmailAddress? email) => RunAsync(async sp =>
        {
            await sp.GetRequiredService<BootstrapAdminHandler>().HandleAsync(email, CancellationToken.None);
            return 0;
        });

        await BootstrapAsync(Email("root@example.com"));
        await BootstrapAsync(Email("root@example.com"));
        _database.Outbox.Messages.Should().ContainSingle();

        _database.Clock.Advance(TimeSpan.FromDays(8));
        await BootstrapAsync(Email("root@example.com"));
        _database.Outbox.Messages.Should().HaveCount(2);

        await AcceptAsync(TokenIn(_database.Outbox.Messages[^1].Link), "Root");
        await BootstrapAsync(null);
        await BootstrapAsync(Email("root@example.com"));
        _database.Outbox.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task Bootstrap_fails_when_there_is_no_admin_and_no_configured_address()
    {
        var act = () => RunAsync(async sp =>
        {
            await sp.GetRequiredService<BootstrapAdminHandler>().HandleAsync(null, CancellationToken.None);
            return 0;
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
