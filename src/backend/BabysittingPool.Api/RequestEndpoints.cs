using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using Microsoft.AspNetCore.Mvc;

namespace BabysittingPool.Api;

internal static class RequestEndpoints
{
    public static void MapRequestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/requests", async (
            HttpContext context, PostRequestBody body, PostRequestHandler handler, CancellationToken cancellationToken) =>
        {
            if (await SessionCookie.CurrentMemberAsync(context, cancellationToken) is not { } member)
            {
                return Problems.Unauthenticated();
            }

            try
            {
                var command = new PostRequestCommand(
                    member.MemberId, body.StartsAt.UtcDateTime, body.ToDetails(), body.Area ?? "", body.Address ?? "");
                return await handler.HandleAsync(command, cancellationToken) is { } view
                    ? Results.Created($"/api/me/requests#{view.Id}", view)
                    : Problems.AccountNotFound();
            }
            catch (DomainException ex)
            {
                return Problems.DomainRule(ex);
            }
            catch (ConcurrencyConflictException)
            {
                return Problems.Conflict();
            }
        })
        .WithName("PostRequest")
        .Produces<RequestView>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/api/me/requests", async (
            HttpContext context,
            ListMyRequestsHandler handler,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            if (await SessionCookie.CurrentMemberAsync(context, cancellationToken) is not { } member)
            {
                return Problems.Unauthenticated();
            }

            try
            {
                return Results.Ok(await handler.HandleAsync(new ListMyRequestsQuery(member.MemberId, cursor, limit), cancellationToken));
            }
            catch (InvalidCursorException)
            {
                return Problems.InvalidRequest("cursor", "invalid");
            }
        })
        .WithName("ListMyRequests")
        .Produces<Page<RequestView>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/requests/open", async (
            HttpContext context,
            ListOpenRequestsHandler handler,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            if (await SessionCookie.CurrentMemberAsync(context, cancellationToken) is not { } member)
            {
                return Problems.Unauthenticated();
            }

            try
            {
                return Results.Ok(await handler.HandleAsync(new ListOpenRequestsQuery(member.MemberId, cursor, limit), cancellationToken));
            }
            catch (InvalidCursorException)
            {
                return Problems.InvalidRequest("cursor", "invalid");
            }
        })
        .WithName("ListOpenRequests")
        .Produces<Page<OpenRequestView>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/me/claims", async (
            HttpContext context,
            ListMyClaimsHandler handler,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            if (await SessionCookie.CurrentMemberAsync(context, cancellationToken) is not { } member)
            {
                return Problems.Unauthenticated();
            }

            try
            {
                return Results.Ok(await handler.HandleAsync(new ListMyClaimsQuery(member.MemberId, cursor, limit), cancellationToken));
            }
            catch (InvalidCursorException)
            {
                return Problems.InvalidRequest("cursor", "invalid");
            }
        })
        .WithName("ListMyClaims")
        .Produces<Page<RequestView>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/requests/{id:guid}/claim", async (
            Guid id, HttpContext context, ClaimRequestHandler handler, CancellationToken cancellationToken) =>
        {
            if (await SessionCookie.CurrentMemberAsync(context, cancellationToken) is not { } member)
            {
                return Problems.Unauthenticated();
            }

            try
            {
                return await handler.HandleAsync(new ClaimRequestCommand(id, member.MemberId), cancellationToken) is { } view
                    ? Results.Ok(view)
                    : Problems.RequestNotFound();
            }
            catch (DomainException ex)
            {
                return Problems.DomainRule(ex);
            }
            catch (ConcurrencyConflictException)
            {
                return Problems.Conflict();
            }
        })
        .WithName("ClaimRequest")
        .Produces<RequestView>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/api/requests/{id:guid}/cancel", async (
            Guid id, HttpContext context, CancelRequestHandler handler, CancellationToken cancellationToken) =>
        {
            if (await SessionCookie.CurrentMemberAsync(context, cancellationToken) is not { } member)
            {
                return Problems.Unauthenticated();
            }

            try
            {
                return await handler.HandleAsync(new CancelRequestCommand(id, member.MemberId), cancellationToken) is { } view
                    ? Results.Ok(view)
                    : Problems.RequestNotFound();
            }
            catch (DomainException ex)
            {
                return Problems.DomainRule(ex);
            }
            catch (ConcurrencyConflictException)
            {
                return Problems.Conflict();
            }
        })
        .WithName("CancelRequest")
        .Produces<RequestView>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/api/requests/{id:guid}/withdraw", async (
            Guid id, HttpContext context, WithdrawClaimHandler handler, CancellationToken cancellationToken) =>
        {
            if (await SessionCookie.CurrentMemberAsync(context, cancellationToken) is not { } member)
            {
                return Problems.Unauthenticated();
            }

            try
            {
                return await handler.HandleAsync(new WithdrawClaimCommand(id, member.MemberId), cancellationToken) is { } view
                    ? Results.Ok(view)
                    : Problems.RequestNotFound();
            }
            catch (DomainException ex)
            {
                return Problems.DomainRule(ex);
            }
            catch (ConcurrencyConflictException)
            {
                return Problems.Conflict();
            }
        })
        .WithName("WithdrawClaim")
        .Produces<RequestView>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/api/requests/quote", async (
            HttpContext context, [AsParameters] QuoteQuery query, CancellationToken cancellationToken) =>
        {
            if (await SessionCookie.CurrentMemberAsync(context, cancellationToken) is null)
            {
                return Problems.Unauthenticated();
            }

            try
            {
                return Results.Ok(QuoteRequest.Handle(query.ToDetails()));
            }
            catch (DomainException ex)
            {
                return Problems.DomainRule(ex);
            }
        })
        .WithName("QuoteRequest")
        .Produces<CostQuote>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private sealed record PostRequestBody(
        DateTimeOffset StartsAt,
        int DurationMinutes,
        int ChildCount,
        BabysittingLocation Location,
        TransportOption Transport,
        bool LunchOrDinner,
        bool Snack,
        string? Area,
        string? Address)
    {
        public CreditRequestDetails ToDetails() =>
            new(DurationMinutes, ChildCount, Location, Transport, Meals(LunchOrDinner, Snack));
    }

    private sealed record QuoteQuery(
        [FromQuery] int DurationMinutes,
        [FromQuery] int ChildCount,
        [FromQuery] BabysittingLocation Location = BabysittingLocation.SittersHouse,
        [FromQuery] TransportOption Transport = TransportOption.None,
        [FromQuery] bool LunchOrDinner = false,
        [FromQuery] bool Snack = false)
    {
        public CreditRequestDetails ToDetails() =>
            new(DurationMinutes, ChildCount, Location, Transport, Meals(LunchOrDinner, Snack));
    }

    private static MealOption Meals(bool lunchOrDinner, bool snack) =>
        (lunchOrDinner ? MealOption.LunchOrDinner : MealOption.None) | (snack ? MealOption.Snack : MealOption.None);
}
