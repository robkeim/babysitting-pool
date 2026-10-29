using AwesomeAssertions;
using BabysittingPool.Application.Notifications;
using BabysittingPool.Application.Offboarding;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure.Persistence;
using backend.tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Application;

public class NotificationTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private Guid _requester;
    private Guid _sitter;
    private Guid _bystander;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateMigratedAsync();
        _requester = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        _sitter = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        _bystander = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private DateTime Now => _database.Clock.GetUtcNow().UtcDateTime;

    private async Task<RequestView> PostAsync(Guid requester, bool atHome = false)
    {
        await using var scope = _database.CreateScope();
        var command = new PostRequestCommand(
            requester, Now.AddHours(10), new CreditRequestDetails(
                120, 1, atHome ? BabysittingLocation.RequestingFamilysHouse : BabysittingLocation.SittersHouse));
        return (await scope.ServiceProvider.GetRequiredService<PostRequestHandler>().HandleAsync(command, default))!;
    }

    private async Task ClaimAsync(Guid requestId, Guid sitter)
    {
        await using var scope = _database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ClaimRequestHandler>()
            .HandleAsync(new ClaimRequestCommand(requestId, sitter), default);
    }

    private async Task<List<NotificationRecord>> AllAsync()
    {
        await using var scope = _database.CreateScope();
        return await _database.NewContext(scope).Notifications.AsNoTracking().ToListAsync();
    }

    private async Task UpdateMemberAsync(Guid memberId, Action<MemberRecord> change)
    {
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        change(await db.Members.SingleAsync(m => m.Id == memberId));
        await db.SaveChangesAsync();
    }

    private async Task<bool> DismissAsync(Guid memberId, Guid notificationId)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DismissNotificationHandler>()
            .HandleAsync(memberId, notificationId, default);
    }

    [Fact]
    public async Task Posting_notifies_every_other_open_member_with_only_the_area_and_start_time()
    {
        var posted = await PostAsync(_requester, atHome: true);

        var rows = await AllAsync();

        rows.Select(n => n.MemberId).Should().BeEquivalentTo([_sitter, _bystander]);
        rows.Should().OnlyContain(n =>
            n.Kind == "request_posted" && n.RequestId == posted.Id && n.CreatedAt == Now
            && n.StartsAt == Now.AddHours(10) && n.SitterId == null && n.Area == "Riverside");
        rows.Select(n => n.Id).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Posting_does_not_notify_the_requester_a_closed_member_or_an_erased_one_but_does_notify_a_dormant_one()
    {
        var closed = await _database.SeedAccountAsync(Credits.FromDecimal(1m), Credits.FromQuarterCredits(0));
        var erased = await _database.SeedAccountAsync(Credits.FromDecimal(1m), Credits.FromQuarterCredits(0));
        await UpdateMemberAsync(closed, m => m.ClosedAt = Now);
        await UpdateMemberAsync(erased, m => { m.ClosedAt = Now; m.DisplayName = "Former member"; m.Email = "Former member"; });
        await UpdateMemberAsync(_bystander, m => m.CreatedAt = Now.AddDays(-365));

        await PostAsync(_requester);

        (await AllAsync()).Select(n => n.MemberId).Should().BeEquivalentTo([_sitter, _bystander]);
    }

    [Fact]
    public async Task A_request_at_the_sitters_house_has_no_area_in_its_notification()
    {
        await PostAsync(_requester);

        (await AllAsync()).Should().OnlyContain(n => n.Area == null);
    }

    [Fact]
    public async Task Claiming_notifies_the_requester_with_the_sitters_id_and_nobody_else()
    {
        var posted = await PostAsync(_requester);
        await using (var scope = _database.CreateScope())
        {
            var db = _database.NewContext(scope);
            await db.Notifications.ExecuteDeleteAsync();
        }

        await ClaimAsync(posted.Id, _sitter);

        var row = (await AllAsync()).Should().ContainSingle().Subject;
        row.MemberId.Should().Be(_requester);
        row.Kind.Should().Be("request_claimed");
        row.RequestId.Should().Be(posted.Id);
        row.SitterId.Should().Be(_sitter);
        row.Area.Should().BeNull();
        row.StartsAt.Should().BeNull();
    }

    private async Task CancelAsync(Guid requestId, Guid member)
    {
        await using var scope = _database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CancelRequestHandler>()
            .HandleAsync(new CancelRequestCommand(requestId, member), default);
    }

    [Fact]
    public async Task Cancelling_a_claimed_request_notifies_the_sitter_with_only_the_start_time()
    {
        var posted = await PostAsync(_requester, atHome: true);
        await ClaimAsync(posted.Id, _sitter);
        await using (var scope = _database.CreateScope())
        {
            await _database.NewContext(scope).Notifications.ExecuteDeleteAsync();
        }

        await CancelAsync(posted.Id, _requester);

        var row = (await AllAsync()).Should().ContainSingle().Subject;
        row.MemberId.Should().Be(_sitter);
        row.Kind.Should().Be("request_cancelled");
        row.RequestId.Should().Be(posted.Id);
        row.StartsAt.Should().Be(Now.AddHours(10));
        row.Area.Should().BeNull();
        row.SitterId.Should().BeNull();
    }

    [Fact]
    public async Task Cancelling_a_posted_request_notifies_nobody()
    {
        var posted = await PostAsync(_requester);
        await using (var scope = _database.CreateScope())
        {
            await _database.NewContext(scope).Notifications.ExecuteDeleteAsync();
        }

        await CancelAsync(posted.Id, _requester);

        (await AllAsync()).Should().BeEmpty();
    }

    private async Task WithdrawAsync(Guid requestId, Guid sitter)
    {
        await using var scope = _database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WithdrawClaimHandler>()
            .HandleAsync(new WithdrawClaimCommand(requestId, sitter), default);
    }

    private async Task ClearNotificationsAsync()
    {
        await using var scope = _database.CreateScope();
        await _database.NewContext(scope).Notifications.ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Withdrawing_a_claim_notifies_the_requester_with_only_the_start_time_and_does_not_renotify_the_pool()
    {
        var posted = await PostAsync(_requester, atHome: true);
        await ClaimAsync(posted.Id, _sitter);
        await ClearNotificationsAsync();

        await WithdrawAsync(posted.Id, _sitter);

        var row = (await AllAsync()).Should().ContainSingle().Subject;
        row.MemberId.Should().Be(_requester);
        row.Kind.Should().Be("request_withdrawn");
        row.RequestId.Should().Be(posted.Id);
        row.StartsAt.Should().Be(Now.AddHours(10));
        row.Area.Should().BeNull();
        row.SitterId.Should().BeNull();
    }

    [Fact]
    public async Task Withdrawing_after_the_claim_cutoff_still_notifies_the_requester_though_the_request_expires()
    {
        var posted = await PostAsync(_requester);
        await ClaimAsync(posted.Id, _sitter);
        await ClearNotificationsAsync();
        _database.Clock.Advance(TimeSpan.FromHours(8));

        await WithdrawAsync(posted.Id, _sitter);

        var row = (await AllAsync()).Should().ContainSingle().Subject;
        row.MemberId.Should().Be(_requester);
        row.Kind.Should().Be("request_withdrawn");
    }

    [Fact]
    public async Task Offboarding_a_sitter_stores_a_withdrawn_notification_for_the_requester()
    {
        var posted = await PostAsync(_requester);
        await ClaimAsync(posted.Id, _sitter);
        await ClearNotificationsAsync();

        await using (var scope = _database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<OffboardMemberHandler>()
                .HandleAsync(new OffboardMemberCommand(_sitter), default);
        }

        (await AllAsync()).Should().ContainSingle(n => n.MemberId == _requester && n.Kind == "request_withdrawn");
    }

    [Fact]
    public async Task A_post_that_loses_to_a_concurrent_change_writes_no_notifications()
    {
        await using var first = _database.CreateScope();
        await using var second = _database.CreateScope();
        var firstStore = first.ServiceProvider.GetRequiredService<IRequestStore>();
        var secondStore = second.ServiceProvider.GetRequiredService<IRequestStore>();
        var firstAccount = (await firstStore.FindAccountAsync(_requester, default))!;
        var secondAccount = (await secondStore.FindAccountAsync(_requester, default))!;
        var details = new CreditRequestDetails(120, 1);
        var one = BabysittingPool.Domain.Requests.Request.Post(
            _requester, firstAccount, details, Now.AddHours(10), null, Now, TimeSpan.FromHours(6), TimeSpan.FromHours(3));
        var two = BabysittingPool.Domain.Requests.Request.Post(
            _requester, secondAccount, details, Now.AddHours(12), null, Now, TimeSpan.FromHours(6), TimeSpan.FromHours(3));
        await firstStore.AddPostedAsync(one, firstAccount, Now, default);

        var act = () => secondStore.AddPostedAsync(two, secondAccount, Now, default);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        (await AllAsync()).Should().OnlyContain(n => n.RequestId == one.Id).And.HaveCount(2);
    }

    [Fact]
    public async Task A_claim_that_loses_the_race_writes_no_notification()
    {
        var posted = await PostAsync(_requester);
        await ClaimAsync(posted.Id, _sitter);

        var act = () => ClaimAsync(posted.Id, _bystander);

        await act.Should().ThrowAsync<Exception>();
        (await AllAsync()).Count(n => n.Kind == "request_claimed").Should().Be(1);
    }

    [Fact]
    public async Task Dismissing_deletes_the_row_and_repeating_it_is_a_success()
    {
        await PostAsync(_requester);
        var mine = (await AllAsync()).Single(n => n.MemberId == _sitter);

        (await DismissAsync(_sitter, mine.Id)).Should().BeTrue();
        (await DismissAsync(_sitter, mine.Id)).Should().BeTrue();

        (await AllAsync()).Select(n => n.MemberId).Should().BeEquivalentTo([_bystander]);
    }

    [Fact]
    public async Task Dismissing_another_members_notification_is_refused_and_leaves_it()
    {
        await PostAsync(_requester);
        var theirs = (await AllAsync()).Single(n => n.MemberId == _bystander);

        (await DismissAsync(_sitter, theirs.Id)).Should().BeFalse();

        (await AllAsync()).Should().Contain(n => n.Id == theirs.Id);
    }

    [Fact]
    public async Task Dismissing_all_deletes_only_the_callers_own()
    {
        await PostAsync(_requester);

        await using (var scope = _database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DismissAllNotificationsHandler>().HandleAsync(_sitter, default);
        }

        (await AllAsync()).Select(n => n.MemberId).Should().BeEquivalentTo([_bystander]);
    }

    [Fact]
    public async Task Listing_is_newest_first_in_pages_and_only_the_members_own()
    {
        await PostAsync(_requester);
        _database.Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await PostAsync(_bystander);
        _database.Clock.Advance(TimeSpan.FromMinutes(5));
        var third = await PostAsync(_bystander);

        await using var scope = _database.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ListNotificationsHandler>();
        var firstPage = await handler.HandleAsync(_sitter, null, 2, default);
        var secondPage = await handler.HandleAsync(_sitter, firstPage.NextCursor, 2, default);

        firstPage.Items.Select(n => n.RequestId).Should().Equal(third.Id, second.Id);
        firstPage.NextCursor.Should().NotBeNull();
        secondPage.Items.Should().ContainSingle().Which.Kind.Should().Be("request_posted");
        secondPage.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Erasing_a_member_deletes_all_their_notifications_and_only_theirs()
    {
        await PostAsync(_requester);
        await using (var scope = _database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<OffboardMemberHandler>()
                .HandleAsync(new OffboardMemberCommand(_sitter), default);
            await scope.ServiceProvider.GetRequiredService<EraseMemberHandler>()
                .HandleAsync(new EraseMemberCommand(_sitter), default);
        }

        (await AllAsync()).Select(n => n.MemberId).Should().BeEquivalentTo([_bystander]);
    }

    [Fact]
    public async Task Settle_deletes_notifications_older_than_ninety_days_and_keeps_newer_ones()
    {
        await PostAsync(_requester);
        var old = (await AllAsync()).Single(n => n.MemberId == _sitter);
        _database.Clock.Advance(TimeSpan.FromDays(89));
        await PostAsync(_bystander);

        _database.Clock.Advance(TimeSpan.FromDays(2));
        await using (var scope = _database.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SettleHandler>().HandleAsync(default);
        }

        var rows = await AllAsync();
        rows.Should().NotContain(n => n.CreatedAt == old.CreatedAt);
        rows.Should().NotBeEmpty().And.OnlyContain(n => n.CreatedAt > old.CreatedAt);
    }
}
