using BabysittingPool.Application.Identity;
using BabysittingPool.Application.Notifications;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using Microsoft.Extensions.Logging;
using DomainCredits = BabysittingPool.Domain.Credits.Credits;

namespace BabysittingPool.Application.Requests;

public sealed record PostRequestCommand(
    Guid RequesterId,
    DateTime StartsAt,
    CreditRequestDetails Details,
    string? Notes = null);

/// <summary>Posts a Request and escrows its cost from the Requester's Available Balance (spec §4.1).</summary>
public sealed partial class PostRequestHandler(
    IRequestStore store, TimeProvider clock, CoopSettings settings, NotificationAnnouncer announcer, ILogger<PostRequestHandler> logger)
{
    /// <summary>Null when the Requester has no Credit Account. Rule violations throw <see cref="DomainException"/>.</summary>
    public async Task<RequestView?> HandleAsync(PostRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await store.FindAccountAsync(command.RequesterId, cancellationToken) is not { } account)
        {
            return null;
        }

        var home = command.Details.Location == BabysittingLocation.RequestingFamilysHouse
            ? await store.FindHomeLocationAsync(command.RequesterId, cancellationToken)
            : null;
        var request = Request.Post(
            command.RequesterId, account, command.Details, command.StartsAt, home,
            now, settings.MinimumLeadTime, settings.ClaimCutoff, command.Notes);
        announcer.Created(await store.AddPostedAsync(request, account, now, cancellationToken));
        LogPosted(request.Id, command.RequesterId);

        return RequestRead.From(request).ToView(now);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "RequestPosted {RequestId} {MemberId}")]
    private partial void LogPosted(Guid requestId, Guid memberId);
}

/// <summary>The live cost preview on the post form: the calculator's answer for the details as entered.</summary>
public static class QuoteRequest
{
    public static CostQuote Handle(CreditRequestDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var cost = CreditCostCalculator.Calculate(details);
        var prices = CreditCostCalculator.OptionPrices(details.ChildCount);
        return new CostQuote(
            Quarters(cost.Total),
            new CostLines(
                Quarters(cost.HourlyRate), Quarters(cost.TimeCost), Quarters(cost.LocationPremium),
                Quarters(cost.TransportFee), Quarters(cost.LunchFee), Quarters(cost.SnackFee), Quarters(cost.DinnerFee)),
            new CostOptionPrices(
                Quarters(prices.FamilyHouse), Quarters(prices.OneWayTransport), Quarters(prices.RoundTripTransport),
                Quarters(prices.Lunch), Quarters(prices.Snack), Quarters(prices.Dinner),
                Quarters(prices.LunchPerChild), Quarters(prices.SnackPerChild), Quarters(prices.DinnerPerChild),
                Quarters(prices.SecondChildHourly), Quarters(prices.AdditionalChildHourly)));
    }

    private static long Quarters(decimal credits) => DomainCredits.FromDecimal(credits).QuarterCredits;
}
