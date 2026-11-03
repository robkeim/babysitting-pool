using AwesomeAssertions;
using BabysittingPool.Application.Identity;
using BabysittingPool.Application.Offboarding;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using BabysittingPool.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Tests.Application;

public class EraseMemberHandlerTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private Guid _leaver;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateMigratedAsync();
        _leaver = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0), Credits.FromDecimal(10m));
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private DateTime Now => _database.Clock.GetUtcNow().UtcDateTime;

    private async Task<bool> EraseAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<EraseMemberHandler>()
            .HandleAsync(new EraseMemberCommand(memberId), default);
    }

    private async Task OffboardAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OffboardMemberHandler>()
            .HandleAsync(new OffboardMemberCommand(memberId), default);
    }

    private async Task<MemberRecord> MemberAsync(Guid memberId)
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).Members.AsNoTracking().SingleAsync(m => m.Id == memberId);
    }

    private async Task<Guid> PostAsync(Guid requester, string? notes = null)
    {
        await using var scope = _database.CreateScope();
        var command = new PostRequestCommand(
            requester, Now.AddHours(10), new CreditRequestDetails(120, 1, BabysittingLocation.RequestingFamilysHouse), notes);
        return (await scope.ServiceProvider.GetRequiredService<PostRequestHandler>().HandleAsync(command, default))!.Id;
    }

    private async Task AddInvitationAsync(string email)
    {
        EmailAddress.TryParse(email, out var address).Should().BeTrue();
        await using var scope = _database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IIdentityStore>().AddInvitationAsync(
            Invitation.Issue(address, false, Secrets.Hash(Secrets.Generate()), Now, TimeSpan.FromDays(7)), default);
    }

    [Fact]
    public async Task Erasing_a_closed_member_replaces_their_name_and_email_and_keeps_the_ledger()
    {
        await OffboardAsync(_leaver);

        (await EraseAsync(_leaver)).Should().BeTrue();

        var member = await MemberAsync(_leaver);
        member.Email.Should().Be("Former member");
        member.DisplayName.Should().Be("Former member");
        member.ClosedAt.Should().NotBeNull();
        await using var scope = _database.CreateScope();
        var ledger = await _database.NewContext(scope).LedgerEntries.AsNoTracking().Where(e => e.MemberId == _leaver).ToListAsync();
        ledger.Select(e => e.Kind).Should().BeEquivalentTo([LedgerEntryKind.Grant, LedgerEntryKind.Burn]);
    }

    [Fact]
    public async Task Erasing_clears_the_profile_and_the_address_of_the_members_requests_and_leaves_other_members_alone()
    {
        var other = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var theirs = await PostAsync(_leaver, "the youngest has a cold");
        var others = await PostAsync(other, "allergic to peanuts");
        await OffboardAsync(_leaver);

        await EraseAsync(_leaver);

        await using var scope = _database.CreateScope();
        var requests = _database.NewContext(scope).Requests.AsNoTracking();
        (await requests.SingleAsync(r => r.Id == theirs)).Address.Should().BeNull();
        (await requests.SingleAsync(r => r.Id == theirs)).Instructions.Should().BeNull();
        (await requests.SingleAsync(r => r.Id == theirs)).Notes.Should().BeNull();
        var erased = await _database.NewContext(scope).Members.AsNoTracking().SingleAsync(m => m.Id == _leaver);
        erased.Commune.Should().BeNull();
        erased.InseeCode.Should().BeNull();
        erased.DepartmentNumber.Should().BeNull();
        erased.Postcode.Should().BeNull();
        erased.Instructions.Should().BeNull();
        erased.Address.Should().BeNull();
        (await requests.SingleAsync(r => r.Id == others)).Address.Should().Be("12 River Rd");
        (await requests.SingleAsync(r => r.Id == others)).Instructions.Should().Be("ring twice");
        (await requests.SingleAsync(r => r.Id == others)).Notes.Should().Be("allergic to peanuts");
    }

    [Fact]
    public async Task Erasing_a_sitter_leaves_the_notes_of_the_requests_they_claimed()
    {
        var requester = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var request = await PostAsync(requester, "the youngest has a cold");
        await using (var scope = _database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ClaimRequestHandler>()
                .HandleAsync(new ClaimRequestCommand(request, _leaver), default);
        }

        await OffboardAsync(_leaver);
        await EraseAsync(_leaver);

        await using var check = _database.CreateScope();
        (await _database.NewContext(check).Requests.AsNoTracking().SingleAsync(r => r.Id == request))
            .Notes.Should().Be("the youngest has a cold");
    }

    [Fact]
    public async Task Erasing_deletes_unused_invitations_and_sign_in_links_for_that_member_only()
    {
        var other = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var email = (await MemberAsync(_leaver)).Email;
        await AddInvitationAsync(email);
        await AddInvitationAsync("someone.else@example.test");
        await using (var scope = _database.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
            await identity.AddSignInLinkAsync(SignInLink.Issue(_leaver, Secrets.Hash(Secrets.Generate()), Now, TimeSpan.FromMinutes(15)), default);
            await identity.AddSignInLinkAsync(SignInLink.Issue(other, Secrets.Hash(Secrets.Generate()), Now, TimeSpan.FromMinutes(15)), default);
        }

        await OffboardAsync(_leaver);
        await EraseAsync(_leaver);

        await using var after = _database.CreateScope();
        var db = _database.NewContext(after);
        (await db.Invitations.Select(i => i.Email).ToListAsync()).Should().BeEquivalentTo(["someone.else@example.test"]);
        (await db.SignInLinks.Select(l => l.MemberId).ToListAsync()).Should().BeEquivalentTo([other]);
    }

    [Fact]
    public async Task Erasing_replaces_the_email_on_the_accepted_invitation_and_leaves_other_invitations_alone()
    {
        var email = (await MemberAsync(_leaver)).Email;
        await AddInvitationAsync(email);
        await AddInvitationAsync("someone.else@example.test");
        await using (var scope = _database.CreateScope())
        {
            await _database.NewContext(scope).Invitations
                .Where(i => i.Email == email)
                .ExecuteUpdateAsync(i => i.SetProperty(x => x.ConsumedAt, (DateTime?)Now).SetProperty(x => x.AcceptedByMemberId, (Guid?)_leaver));
        }

        await OffboardAsync(_leaver);
        await EraseAsync(_leaver);

        await using var after = _database.CreateScope();
        var emails = await _database.NewContext(after).Invitations.AsNoTracking().Select(i => i.Email).ToListAsync();
        emails.Should().BeEquivalentTo(["Former member", "someone.else@example.test"]);
    }

    [Fact]
    public async Task Erasing_an_earlier_member_leaves_the_accepted_invitation_of_a_later_member_with_the_same_email()
    {
        var email = (await MemberAsync(_leaver)).Email;
        var later = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await AddInvitationAsync(email);
        await AddInvitationAsync(email);
        await using (var scope = _database.CreateScope())
        {
            var ids = await _database.NewContext(scope).Invitations.AsNoTracking().OrderBy(i => i.Id).Select(i => i.Id).ToListAsync();
            var db = _database.NewContext(scope);
            await db.Invitations.Where(i => i.Id == ids[0])
                .ExecuteUpdateAsync(i => i.SetProperty(x => x.ConsumedAt, (DateTime?)Now).SetProperty(x => x.AcceptedByMemberId, (Guid?)_leaver));
            await db.Invitations.Where(i => i.Id == ids[1])
                .ExecuteUpdateAsync(i => i.SetProperty(x => x.ConsumedAt, (DateTime?)Now).SetProperty(x => x.AcceptedByMemberId, (Guid?)later));
        }

        await OffboardAsync(_leaver);
        await EraseAsync(_leaver);

        await using var after = _database.CreateScope();
        var emails = await _database.NewContext(after).Invitations.AsNoTracking().Select(i => i.Email).ToListAsync();
        emails.Should().BeEquivalentTo(["Former member", email]);
    }

    [Fact]
    public async Task An_open_member_cannot_be_erased()
    {
        var before = await MemberAsync(_leaver);

        var act = () => EraseAsync(_leaver);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("member_not_closed");
        (await MemberAsync(_leaver)).Email.Should().Be(before.Email);
    }

    [Fact]
    public async Task Erasing_twice_changes_nothing()
    {
        await OffboardAsync(_leaver);
        await EraseAsync(_leaver);

        (await EraseAsync(_leaver)).Should().BeTrue();

        (await MemberAsync(_leaver)).Email.Should().Be("Former member");
    }

    [Fact]
    public async Task An_unknown_member_is_not_found()
    {
        (await EraseAsync(Guid.NewGuid())).Should().BeFalse();
    }
}
