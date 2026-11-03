using AwesomeAssertions;
using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
using BabysittingPool.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Tests.Infrastructure;

public class IdentityStoreTests : IAsyncLifetime
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private TestDatabase _database = null!;
    private Guid _memberId;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateMigratedAsync();
        _memberId = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0), Credits.FromDecimal(10m));
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private DateTime Now => _database.Clock.GetUtcNow().UtcDateTime;

    private async Task<SignInLink> IssueLinkAsync()
    {
        var link = SignInLink.Issue(_memberId, Guid.NewGuid().ToString("N"), Now, Lifetime);
        await using var scope = _database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IIdentityStore>().AddSignInLinkAsync(link, default);
        return link;
    }

    private async Task<bool> RedeemAsync(SignInLink link, Session session)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IIdentityStore>()
            .TryRedeemSignInLinkAsync(link, session, Now, default);
    }

    [Fact]
    public async Task Accepting_an_Invitation_for_an_address_that_is_already_a_Member_is_a_lost_race_not_an_error()
    {
        await using var scope = _database.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
        var existing = (await store.FindMemberAsync(_memberId, default))!;
        var invitation = Invitation.Issue(existing.Email, false, "invite", Now, Lifetime);
        await store.AddInvitationAsync(invitation, default);
        var joiner = Member.Join(existing.Email, DisplayName.Restore("Pat"), false, Now);

        var accepted = await store.TryAcceptInvitationAsync(
            invitation, joiner, Session.Start(joiner.Id, "joiner", Now), Now, default);

        accepted.Should().BeFalse();
    }

    [Fact]
    public async Task A_database_failure_other_than_a_lost_race_surfaces()
    {
        var link = await IssueLinkAsync();

        var act = () => RedeemAsync(link, Session.Start(Guid.NewGuid(), "orphan", Now));

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
