using BabysittingPool.Application.Identity;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using DomainCredits = BabysittingPool.Domain.Credits.Credits;

namespace BabysittingPool.Application.Credits;

/// <summary>The co-op rules every Member can read, the same numbers the engine enforces (spec §7). No personal data.</summary>
public sealed record RulesView(
    long StartingBalanceQuarterCredits,
    int DurationIncrementMinutes,
    int MinimumLeadTimeMinutes,
    int ClaimCutoffMinutes,
    DayOfWeek DemurrageDay,
    string DemurrageTime,
    string TimeZone,
    int DemurrageGraceDays,
    long DemurrageChargeQuarterCredits,
    int DormancyDays,
    CostOptionPrices Prices,
    IReadOnlyList<PricingExample> Examples);

/// <summary>A worked example: the inputs, and the quote the domain calculator gives for them.</summary>
public sealed record PricingExample(
    int DurationMinutes,
    int ChildCount,
    BabysittingLocation Location,
    TransportOption Transport,
    bool Lunch,
    bool Snack,
    bool Dinner,
    CostQuote Quote);

public sealed class GetRulesHandler(CoopSettings settings)
{
    private static readonly CreditRequestDetails[] ExampleDetails =
    [
        new(60, 1),
        new(90, 2, BabysittingLocation.RequestingFamilysHouse, TransportOption.None, MealOption.Lunch),
        new(120, 3, BabysittingLocation.SittersHouse, TransportOption.RoundTrip, MealOption.Snack),
    ];

    public RulesView Handle()
    {
        var examples = ExampleDetails.Select(details => new PricingExample(
            details.DurationMinutes, details.ChildCount, details.Location, details.Transport,
            details.Meals.HasFlag(MealOption.Lunch), details.Meals.HasFlag(MealOption.Snack),
            details.Meals.HasFlag(MealOption.Dinner), QuoteRequest.Handle(details))).ToList();

        return new RulesView(
            DomainCredits.FromDecimal(CreditAccount.StartingBalance).QuarterCredits,
            CreditRequestDetails.DurationIncrementMinutes,
            (int)settings.MinimumLeadTime.TotalMinutes,
            (int)settings.ClaimCutoff.TotalMinutes,
            settings.DemurrageDay,
            settings.DemurrageTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            settings.TimeZone.Id,
            (int)Demurrage.GracePeriod.TotalDays,
            DomainCredits.FromDecimal(Demurrage.WeeklyPenalty).QuarterCredits,
            (int)settings.DormancyThreshold.TotalDays,
            QuoteRequest.Handle(ExampleDetails[0]).Options,
            examples);
    }
}
