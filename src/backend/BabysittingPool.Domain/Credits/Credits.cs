namespace BabysittingPool.Domain.Credits;

/// <summary>
/// A non-negative amount of credits in 0.25 steps (ADR 0001), held as whole quarter-credits.
/// </summary>
public readonly record struct Credits
{
    private const int QuartersPerCredit = 4;

    private Credits(long quarterCredits) => QuarterCredits = quarterCredits;

    public long QuarterCredits { get; }

    public decimal Value => (decimal)QuarterCredits / QuartersPerCredit;

    public static Credits FromQuarterCredits(long quarterCredits)
    {
        if (quarterCredits < 0) throw new DomainException("Credits cannot be negative.");
        return new Credits(quarterCredits);
    }

    public static Credits FromDecimal(decimal credits)
    {
        var quarters = credits * QuartersPerCredit;
        if (quarters != decimal.Truncate(quarters))
            throw new DomainException($"Credits must be a multiple of 0.25, got {credits}.");
        return FromQuarterCredits((long)quarters);
    }
}
