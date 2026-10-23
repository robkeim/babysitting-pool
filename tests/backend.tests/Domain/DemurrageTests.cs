using AwesomeAssertions;
using BabysittingPool.Domain.Credits;

namespace backend.tests.Domain;

public class DemurrageTests
{
    private static readonly DateTime MemberSince = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime FirstBoundary = new(2026, 3, 23, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime Boundary(int weeksAfterFirst) => FirstBoundary.AddDays(7 * weeksAfterFirst);

    private static IReadOnlyList<DemurrageCharge> Apply(
        CreditAccount account, IEnumerable<DateTime> boundaries, params DateTime[] completed) =>
        Demurrage.Apply(account, MemberSince, completed, boundaries);

    [Fact]
    public void Exactly_21_days_without_activity_is_not_charged()
    {
        var last = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

        Demurrage.IsDue(last, last.AddDays(21)).Should().BeFalse();
    }

    [Fact]
    public void Day_22_without_activity_is_charged()
    {
        var last = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

        Demurrage.IsDue(last, last.AddDays(21).AddTicks(1)).Should().BeTrue();
        Demurrage.IsDue(last, last.AddDays(22)).Should().BeTrue();
    }

    [Fact]
    public void A_new_Member_is_charged_from_the_boundary_more_than_21_days_after_account_creation()
    {
        var account = CreditAccount.Restore(10m, 0m);

        var charges = Apply(account, [Boundary(-1), FirstBoundary]);

        charges.Should().Equal(new DemurrageCharge(FirstBoundary, 2m));
        account.Available.Should().Be(8m);
    }

    [Fact]
    public void Each_missed_boundary_is_charged_in_order()
    {
        var account = CreditAccount.Restore(10m, 0m);

        var charges = Apply(account, [Boundary(2), FirstBoundary, Boundary(1)]);

        charges.Select(c => c.Boundary).Should().Equal(FirstBoundary, Boundary(1), Boundary(2));
        account.Available.Should().Be(4m);
    }

    [Fact]
    public void The_charge_stops_at_zero_and_never_goes_negative()
    {
        var account = CreditAccount.Restore(1m, 0m);

        var charges = Apply(account, [FirstBoundary, Boundary(1)]);

        charges.Should().Equal(new DemurrageCharge(FirstBoundary, 1m));
        account.Available.Should().Be(0m);
    }

    [Fact]
    public void An_empty_account_records_no_charge()
    {
        var account = CreditAccount.Restore(0m, 3m);

        Apply(account, [FirstBoundary]).Should().BeEmpty();
        account.Available.Should().Be(0m);
    }

    [Fact]
    public void Escrow_is_never_charged()
    {
        var account = CreditAccount.Restore(1m, 5m);

        Apply(account, [FirstBoundary, Boundary(1)]);

        account.Available.Should().Be(0m);
        account.Escrowed.Should().Be(5m);
    }

    [Fact]
    public void A_completed_Request_resets_the_window()
    {
        var account = CreditAccount.Restore(10m, 0m);
        var completedAt = Boundary(1).AddDays(-1);

        var charges = Apply(account, [FirstBoundary, Boundary(1), Boundary(2), Boundary(3), Boundary(4)], completedAt);

        // Charged before the Request; then 1, 8 and 15 days after it are active; 22 days after it is charged again.
        charges.Select(c => c.Boundary).Should().Equal(FirstBoundary, Boundary(4));
        account.Available.Should().Be(6m);
    }

    [Fact]
    public void Each_boundary_is_judged_as_of_its_own_time()
    {
        var account = CreditAccount.Restore(10m, 0m);
        var completedAfterTheFirstBoundary = FirstBoundary.AddDays(1);

        var charges = Apply(account, [FirstBoundary], completedAfterTheFirstBoundary);

        charges.Should().HaveCount(1);
    }
}

public class DemurrageScheduleTests
{
    private static readonly DemurrageSchedule MondayMidnightUtc = new(DayOfWeek.Monday, TimeOnly.MinValue, TimeZoneInfo.Utc);

    [Fact]
    public void Lists_the_Mondays_after_the_start_up_to_and_including_now()
    {
        var after = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var upTo = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

        MondayMidnightUtc.BoundariesBetween(after, upTo).Should().Equal(
            new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void A_boundary_exactly_at_the_start_is_excluded()
    {
        var monday = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);

        MondayMidnightUtc.BoundariesBetween(monday, monday.AddDays(7)).Should().Equal(monday.AddDays(7));
    }

    [Fact]
    public void Follows_the_co_op_time_zone_across_daylight_saving()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var schedule = new DemurrageSchedule(DayOfWeek.Monday, TimeOnly.MinValue, berlin);

        var boundaries = schedule.BoundariesBetween(
            new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 4, 7, 0, 0, 0, DateTimeKind.Utc)).ToList();

        // Winter time (UTC+1) before 29 March 2026, summer time (UTC+2) after it.
        boundaries.Should().Equal(
            new DateTime(2026, 3, 22, 23, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 29, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 5, 22, 0, 0, DateTimeKind.Utc));
    }
}
