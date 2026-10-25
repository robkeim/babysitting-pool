using AwesomeAssertions;
using BabysittingPool.Application;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using backend.tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.tests.Application;

public class PostRequestHandlerTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private DateTime Start => _database.Clock.GetUtcNow().UtcDateTime.AddHours(10);

    private PostRequestCommand Command(Guid memberId, DateTime? startsAt = null, int durationMinutes = 120) => new(
        memberId, startsAt ?? Start, new CreditRequestDetails(durationMinutes, 1), "Riverside", "12 River Rd");

    [Fact]
    public async Task Posting_takes_the_cost_into_escrow_and_records_the_request_and_the_ledger_entry()
    {
        var memberId = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));

        var view = await PostAsync(Command(memberId));

        view.Should().NotBeNull();
        view!.Status.Should().Be(RequestStatus.Posted);
        view.CostQuarterCredits.Should().Be(8);
        view.ClaimCutoffAt.Should().Be(Start.AddHours(-3));
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        var account = await db.CreditAccounts.AsNoTracking().SingleAsync(a => a.MemberId == memberId);
        account.Available.Value.Should().Be(8m);
        account.Escrowed.Value.Should().Be(2m);
        var stored = await db.Requests.AsNoTracking().SingleAsync();
        stored.Id.Should().Be(view.Id);
        stored.RequesterId.Should().Be(memberId);
        stored.Address.Should().Be("12 River Rd");
        var ledger = await db.LedgerEntries.AsNoTracking().SingleAsync(e => e.Kind == LedgerEntryKind.Escrow);
        ledger.RequestId.Should().Be(view.Id);
        ledger.MemberId.Should().Be(memberId);
        ledger.Amount.QuarterCredits.Should().Be(8);
    }

    [Theory]
    [InlineData(0, 0, "no_available_credits")]
    [InlineData(7, 0, "insufficient_credits")]
    public async Task Posting_is_blocked_when_available_balance_is_below_the_cost(
        long availableQuarters, long escrowedQuarters, string code)
    {
        var memberId = await _database.SeedAccountAsync(
            Credits.FromQuarterCredits(availableQuarters), Credits.FromQuarterCredits(escrowedQuarters));

        var act = () => PostAsync(Command(memberId));

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(code);
        await AssertNothingWasSavedAsync(memberId, availableQuarters);
    }

    [Fact]
    public async Task Posting_inside_the_minimum_lead_time_is_blocked()
    {
        var memberId = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var tooSoon = _database.Clock.GetUtcNow().UtcDateTime.AddHours(5);

        var act = () => PostAsync(Command(memberId, tooSoon));

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("below_minimum_lead_time");
        await AssertNothingWasSavedAsync(memberId, 40);
    }

    [Fact]
    public async Task Posting_answers_null_for_a_member_without_an_account()
    {
        var view = await PostAsync(Command(Guid.NewGuid()));

        view.Should().BeNull();
    }

    [Fact]
    public async Task Two_posts_racing_on_the_same_balance_cannot_both_win()
    {
        var memberId = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await using var first = _database.CreateScope();
        await using var second = _database.CreateScope();
        var firstStore = first.ServiceProvider.GetRequiredService<IRequestStore>();
        var secondStore = second.ServiceProvider.GetRequiredService<IRequestStore>();
        var now = _database.Clock.GetUtcNow().UtcDateTime;
        var firstAccount = (await firstStore.FindAccountAsync(memberId, default))!;
        var secondAccount = (await secondStore.FindAccountAsync(memberId, default))!;
        var firstRequest = Request.Post(
            memberId, firstAccount, new CreditRequestDetails(120, 1), Start, "A", "A", now, TimeSpan.FromHours(6), TimeSpan.FromHours(3));
        var secondRequest = Request.Post(
            memberId, secondAccount, new CreditRequestDetails(120, 1), Start, "B", "B", now, TimeSpan.FromHours(6), TimeSpan.FromHours(3));
        await firstStore.AddPostedAsync(firstRequest, firstAccount, now, default);

        var act = () => secondStore.AddPostedAsync(secondRequest, secondAccount, now, default);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        (await db.Requests.CountAsync()).Should().Be(1);
        (await db.CreditAccounts.AsNoTracking().SingleAsync(a => a.MemberId == memberId)).Escrowed.Value.Should().Be(2m);
    }

    [Fact]
    public async Task My_requests_lists_only_the_members_own_requests_newest_first_with_their_state()
    {
        var memberId = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var other = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        var older = await PostAsync(Command(memberId));
        _database.Clock.Advance(TimeSpan.FromMinutes(5));
        var newer = await PostAsync(Command(memberId, Start.AddDays(1)));
        await PostAsync(Command(other));

        var page = await ListAsync(memberId);

        page.Items.Select(r => r.Id).Should().Equal(newer!.Id, older!.Id);
        page.Items.Should().OnlyContain(r => r.Status == RequestStatus.Posted);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task My_requests_shows_an_overdue_posted_request_as_expired()
    {
        var memberId = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        await PostAsync(Command(memberId));
        _database.Clock.Advance(TimeSpan.FromHours(7));

        var page = await ListAsync(memberId);

        page.Items.Single().Status.Should().Be(RequestStatus.Expired);
    }

    [Fact]
    public async Task My_requests_pages_with_a_cursor()
    {
        var memberId = await _database.SeedAccountAsync(Credits.FromDecimal(10m), Credits.FromQuarterCredits(0));
        for (var i = 0; i < 3; i++)
        {
            await PostAsync(Command(memberId, durationMinutes: 15));
            _database.Clock.Advance(TimeSpan.FromMinutes(5));
        }

        var first = await ListAsync(memberId, limit: 2);
        var second = await ListAsync(memberId, first.NextCursor, limit: 2);

        first.Items.Should().HaveCount(2);
        first.NextCursor.Should().NotBeNullOrEmpty();
        second.Items.Should().HaveCount(1);
        second.NextCursor.Should().BeNull();
        first.Items.Concat(second.Items).Select(r => r.Id).Should().OnlyHaveUniqueItems();
    }

    private async Task AssertNothingWasSavedAsync(Guid memberId, long expectedAvailableQuarters)
    {
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        (await db.Requests.CountAsync()).Should().Be(0);
        (await db.LedgerEntries.CountAsync(e => e.Kind == LedgerEntryKind.Escrow)).Should().Be(0);
        (await db.CreditAccounts.AsNoTracking().SingleAsync(a => a.MemberId == memberId))
            .Available.QuarterCredits.Should().Be(expectedAvailableQuarters);
    }

    private async Task<RequestView?> PostAsync(PostRequestCommand command)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PostRequestHandler>().HandleAsync(command, default);
    }

    private async Task<Page<RequestView>> ListAsync(Guid member, string? cursor = null, int? limit = null)
    {
        await using var scope = _database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestReader>()
            .ListByRequesterAsync(member, _database.Clock.GetUtcNow().UtcDateTime, PageRequest.From(cursor, limit), default);
    }
}
