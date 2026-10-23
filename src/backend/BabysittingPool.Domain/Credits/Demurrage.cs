namespace BabysittingPool.Domain.Credits;

/// <summary>One Demurrage charge, tied to the weekly boundary it belongs to (spec §5).</summary>
public sealed record DemurrageCharge(DateTime Boundary, decimal Amount);

/// <summary>
/// The Inactivity Policy's penalty (spec §5). Pure: the boundaries and the clock are passed in, so a Member's charges
/// depend only on their history.
/// </summary>
public static class Demurrage
{
    public const decimal WeeklyPenalty = 2.0m;

    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(21);

    /// <summary>True when the last activity is more than 21 days before the boundary; exactly 21 days is not.</summary>
    public static bool IsDue(DateTime lastActivity, DateTime boundary) => boundary - lastActivity > GracePeriod;

    /// <summary>
    /// Charges the account for each boundary, in order, each judged as of its own time: the last activity is the
    /// latest of <paramref name="completedStartTimes"/> not after the boundary, or <paramref name="memberSince"/> if
    /// none. A charge is min(2.0, Available), and nothing is recorded once the account is empty.
    /// </summary>
    public static IReadOnlyList<DemurrageCharge> Apply(
        CreditAccount account,
        DateTime memberSince,
        IReadOnlyCollection<DateTime> completedStartTimes,
        IEnumerable<DateTime> boundaries)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(completedStartTimes);
        ArgumentNullException.ThrowIfNull(boundaries);

        var charges = new List<DemurrageCharge>();
        foreach (var boundary in boundaries.Order())
        {
            var lastActivity = completedStartTimes.Where(t => t <= boundary).DefaultIfEmpty(memberSince).Max();
            if (!IsDue(lastActivity, boundary))
            {
                continue;
            }

            var charged = account.Burn(WeeklyPenalty);
            if (charged > 0m)
            {
                charges.Add(new DemurrageCharge(boundary, charged));
            }
        }

        return charges;
    }
}
