using AwesomeAssertions;
using BabysittingPool.Application;
using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using backend.tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Application;

public class ClaimRequestHandlerTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private Guid _requester;
    private Guid _sitter;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateMigratedAsync();
        _requester = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        _sitter = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private DateTime Now => _database.Clock.GetUtcNow().UtcDateTime;

    private async Task<RequestView> PostAsync(Guid requester, int hoursAhead = 10, bool atHome = false)
    {
        await using var scope = _database.CreateScope();
        var command = new PostRequestCommand(
            requester, Now.AddHours(hoursAhead), new CreditRequestDetails(
                120, 1, atHome ? BabysittingLocation.RequestingFamilysHouse : BabysittingLocation.SittersHouse));
        return (await scope.ServiceProvider.GetRequiredService<PostRequestHandler>().HandleAsync(command, default))!;
    }

    private async Task<RequestView?> ClaimAsync(Guid requestId, Guid sitter)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ClaimRequestHandler>()
            .HandleAsync(new ClaimRequestCommand(requestId, sitter), default);
    }

    private async Task<Page<OpenRequestView>> OpenAsync(Guid member, string? cursor = null, int? limit = null)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestReader>()
            .ListOpenAsync(member, _database.Clock.GetUtcNow().UtcDateTime, PageRequest.From(cursor, limit), default);
    }

    private async Task<Page<RequestView>> ClaimsAsync(Guid member)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestReader>()
            .ListClaimedByAsync(member, _database.Clock.GetUtcNow().UtcDateTime, PageRequest.From(null, null), default);
    }

    [Fact]
    public async Task Claiming_records_the_sitter_and_shows_the_exact_address_to_them()
    {
        var posted = await PostAsync(_requester, atHome: true);

        var claimed = await ClaimAsync(posted.Id, _sitter);

        claimed!.Status.Should().Be(RequestStatus.Claimed);
        claimed.Address.Should().Be("12 River Rd");
        await using var scope = _database.CreateScope();
        var stored = await _database.NewContext(scope).Requests.AsNoTracking().SingleAsync();
        stored.Status.Should().Be(RequestStatus.Claimed);
        stored.SitterId.Should().Be(_sitter);
    }

    [Fact]
    public async Task Claiming_does_not_move_any_credits()
    {
        var posted = await PostAsync(_requester);

        await ClaimAsync(posted.Id, _sitter);

        await using var scope = _database.CreateScope();
        var accounts = await _database.NewContext(scope).CreditAccounts.AsNoTracking().ToListAsync();
        accounts.Single(a => a.MemberId == _requester).Escrowed.Value.Should().Be(2m);
        accounts.Single(a => a.MemberId == _sitter).Available.Value.Should().Be(10m);
    }

    [Fact]
    public async Task Claiming_an_unknown_request_answers_null()
    {
        (await ClaimAsync(Guid.NewGuid(), _sitter)).Should().BeNull();
    }

    [Fact]
    public async Task A_member_cannot_claim_their_own_request()
    {
        var posted = await PostAsync(_requester);

        var act = () => ClaimAsync(posted.Id, _requester);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("own_request");
    }

    [Fact]
    public async Task A_claimed_request_cannot_be_claimed_by_another_sitter()
    {
        var posted = await PostAsync(_requester);
        await ClaimAsync(posted.Id, _sitter);
        var later = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));

        var act = () => ClaimAsync(posted.Id, later);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("invalid_state");
    }

    [Fact]
    public async Task Claiming_at_the_claim_cutoff_is_rejected_by_the_clock_even_before_settle_has_expired_it()
    {
        var posted = await PostAsync(_requester);
        _database.Clock.SetUtcNow(new DateTimeOffset(posted.ClaimCutoffAt, TimeSpan.Zero));

        var act = () => ClaimAsync(posted.Id, _sitter);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("claim_cutoff_passed");
    }

    [Fact]
    public async Task A_sitter_cannot_claim_a_request_that_overlaps_one_they_hold()
    {
        await ClaimAsync((await PostAsync(_requester, hoursAhead: 10)).Id, _sitter);
        var overlapping = await PostAsync(_requester, hoursAhead: 11);

        var act = () => ClaimAsync(overlapping.Id, _sitter);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("sitter_overlap");
    }

    [Fact]
    public async Task A_sitter_can_claim_a_request_that_starts_when_their_other_claim_ends()
    {
        await ClaimAsync((await PostAsync(_requester, hoursAhead: 10)).Id, _sitter);
        var adjacent = await PostAsync(_requester, hoursAhead: 12);

        var claimed = await ClaimAsync(adjacent.Id, _sitter);

        claimed!.Status.Should().Be(RequestStatus.Claimed);
    }

    [Fact]
    public async Task Two_sitters_can_claim_overlapping_requests()
    {
        var other = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await ClaimAsync((await PostAsync(_requester, hoursAhead: 10)).Id, _sitter);

        var claimed = await ClaimAsync((await PostAsync(_requester, hoursAhead: 11)).Id, other);

        claimed!.Status.Should().Be(RequestStatus.Claimed);
    }

    [Fact]
    public async Task One_sitter_racing_for_two_overlapping_requests_wins_only_one()
    {
        var first = await PostAsync(_requester, hoursAhead: 10);
        var second = await PostAsync(_requester, hoursAhead: 11);

        var results = await Task.WhenAll(
            Attempt(first.Id), Attempt(second.Id));

        results.Count(claimed => claimed).Should().Be(1);
        await using var scope = _database.CreateScope();
        (await _database.NewContext(scope).Requests.AsNoTracking().CountAsync(r => r.Status == RequestStatus.Claimed))
            .Should().Be(1);

        async Task<bool> Attempt(Guid requestId)
        {
            try
            {
                await ClaimAsync(requestId, _sitter);
                return true;
            }
            catch (DomainException ex) when (ex.Code == "sitter_overlap")
            {
                return false;
            }
        }
    }

    [Fact]
    public async Task Two_sitters_racing_for_the_same_request_cannot_both_win()
    {
        var posted = await PostAsync(_requester);
        var other = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await using var first = _database.CreateScope();
        await using var second = _database.CreateScope();
        var firstStore = first.ServiceProvider.GetRequiredService<IRequestStore>();
        var secondStore = second.ServiceProvider.GetRequiredService<IRequestStore>();
        var firstRequest = (await firstStore.FindRequestAsync(posted.Id, default))!;
        var secondRequest = (await secondStore.FindRequestAsync(posted.Id, default))!;
        firstRequest.Claim(_sitter, Now, []);
        secondRequest.Claim(other, Now, []);
        await firstStore.SaveClaimedAsync(firstRequest, default);

        var act = () => secondStore.SaveClaimedAsync(secondRequest, default);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        await using var scope = _database.CreateScope();
        (await _database.NewContext(scope).Requests.AsNoTracking().SingleAsync()).SitterId.Should().Be(_sitter);
    }

    [Fact]
    public async Task Open_requests_list_the_earliest_start_first_with_the_rough_area_and_without_own_requests()
    {
        var later = await PostAsync(_requester, hoursAhead: 20);
        var sooner = await PostAsync(_requester, hoursAhead: 8, atHome: true);
        var mine = await PostAsync(_sitter, hoursAhead: 9);

        var page = await OpenAsync(_sitter);

        page.Items.Select(r => r.Id).Should().Equal(sooner.Id, later.Id);
        page.Items.Select(r => r.Id).Should().NotContain(mine.Id);
        page.Items[0].Area.Should().Be("Riverside");
        page.Items[0].CostQuarterCredits.Should().Be(10);
        page.Items[1].Area.Should().BeNull();
    }

    [Fact]
    public async Task Open_requests_leave_out_claimed_requests()
    {
        var claimed = await PostAsync(_requester, hoursAhead: 8);
        var open = await PostAsync(_requester, hoursAhead: 9);
        await ClaimAsync(claimed.Id, _sitter);
        var browser = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));

        var page = await OpenAsync(browser);

        page.Items.Select(r => r.Id).Should().Equal(open.Id);
    }

    [Fact]
    public async Task Open_requests_leave_out_those_past_their_claim_cutoff_before_settle_has_expired_them()
    {
        var posted = await PostAsync(_requester);
        _database.Clock.SetUtcNow(new DateTimeOffset(posted.ClaimCutoffAt, TimeSpan.Zero));

        var page = await OpenAsync(_sitter);

        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Open_requests_page_with_a_cursor_in_start_order()
    {
        for (var i = 0; i < 3; i++)
        {
            await PostAsync(_requester, hoursAhead: 8 + i);
        }

        var first = await OpenAsync(_sitter, limit: 2);
        var second = await OpenAsync(_sitter, first.NextCursor, 2);

        first.Items.Should().HaveCount(2);
        first.NextCursor.Should().NotBeNullOrEmpty();
        second.Items.Should().HaveCount(1);
        second.NextCursor.Should().BeNull();
        first.Items.Concat(second.Items).Select(r => r.StartsAt).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task My_claims_list_the_sitters_claimed_requests_with_the_address_and_their_state()
    {
        var posted = await PostAsync(_requester, atHome: true);
        await PostAsync(_requester, hoursAhead: 12);
        await ClaimAsync(posted.Id, _sitter);

        var page = await ClaimsAsync(_sitter);

        var claim = page.Items.Single();
        claim.Id.Should().Be(posted.Id);
        claim.Status.Should().Be(RequestStatus.Claimed);
        claim.Address.Should().Be("12 River Rd");
        (await ClaimsAsync(_requester)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task My_claims_show_a_claim_whose_start_time_has_passed_as_completed()
    {
        var posted = await PostAsync(_requester);
        await ClaimAsync(posted.Id, _sitter);
        _database.Clock.Advance(TimeSpan.FromHours(11));

        var page = await ClaimsAsync(_sitter);

        page.Items.Single().Status.Should().Be(RequestStatus.Completed);
    }
}
