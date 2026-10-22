using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using DomainCredits = BabysittingPool.Domain.Credits.Credits;

namespace BabysittingPool.Application.Requests;

public sealed record PostRequestCommand(
    Guid RequesterId,
    DateTime StartsAt,
    CreditRequestDetails Details,
    string Area,
    string Address);

/// <summary>Posts a Request and escrows its cost from the Requester's Available Balance (spec §4.1).</summary>
public sealed class PostRequestHandler(IRequestStore store, TimeProvider clock, CoopSettings settings)
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

        var request = Request.Post(
            command.RequesterId, account, command.Details, command.StartsAt, command.Area, command.Address,
            now, settings.MinimumLeadTime, settings.ClaimCutoff);
        await store.AddPostedAsync(request, account, now, cancellationToken);

        return RequestRead.From(request).ToView(now);
    }
}

/// <summary>The live cost preview on the post form: the calculator's answer for the details as entered.</summary>
public static class QuoteRequest
{
    public static CostQuote Handle(CreditRequestDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return new CostQuote(DomainCredits.FromDecimal(CreditCostCalculator.Calculate(details).Total).QuarterCredits);
    }
}
