namespace BabysittingPool.Domain.Credits;

/// <summary>
/// The weekly instant Demurrage is charged at: a weekday and a time of day in the co-op time zone (spec §5).
/// </summary>
public sealed record DemurrageSchedule(DayOfWeek Day, TimeOnly Time, TimeZoneInfo Zone)
{
    /// <summary>The boundaries strictly after <paramref name="after"/> and not after <paramref name="upTo"/>, as UTC instants, oldest first.</summary>
    public IEnumerable<DateTime> BoundariesBetween(DateTime after, DateTime upTo)
    {
        var date = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(after, DateTimeKind.Utc), Zone).Date;
        while (date.DayOfWeek != Day)
        {
            date = date.AddDays(1);
        }

        for (; ; date = date.AddDays(7))
        {
            var local = DateTime.SpecifyKind(date + Time.ToTimeSpan(), DateTimeKind.Unspecified);
            if (Zone.IsInvalidTime(local))
            {
                // The clocks skipped this time of day: charge at the first instant after the gap.
                local = local.AddHours(1);
            }

            var boundary = TimeZoneInfo.ConvertTimeToUtc(local, Zone);
            if (boundary > upTo)
            {
                yield break;
            }

            if (boundary > after)
            {
                yield return boundary;
            }
        }
    }
}
