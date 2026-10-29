using BabysittingPool.Application;
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
            var member = context.CurrentMember();

            var command = new PostRequestCommand(
                member.MemberId, body.StartsAt.UtcDateTime, body.ToDetails());
            return await handler.HandleAsync(command, cancellationToken) is { } view
                ? Results.Created($"/api/me/requests#{view.Id}", view)
                : Problems.AccountNotFound();
        })
        .RequireAuthorization()
        .WithName("PostRequest")
        .Produces<RequestView>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/api/me/requests", async (
            HttpContext context,
            IRequestReader reader,
            TimeProvider clock,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var member = context.CurrentMember();

            return Results.Ok(await reader.ListByRequesterAsync(member.MemberId, clock.GetUtcNow().UtcDateTime, PageRequest.From(cursor, limit), cancellationToken));
        })
        .RequireAuthorization()
        .WithName("ListMyRequests")
        .Produces<Page<RequestView>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/requests/open", async (
            HttpContext context,
            IRequestReader reader,
            TimeProvider clock,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var member = context.CurrentMember();

            return Results.Ok(await reader.ListOpenAsync(member.MemberId, clock.GetUtcNow().UtcDateTime, PageRequest.From(cursor, limit), cancellationToken));
        })
        .RequireAuthorization()
        .WithName("ListOpenRequests")
        .Produces<Page<OpenRequestView>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/me/claims", async (
            HttpContext context,
            IRequestReader reader,
            TimeProvider clock,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var member = context.CurrentMember();

            return Results.Ok(await reader.ListClaimedByAsync(member.MemberId, clock.GetUtcNow().UtcDateTime, PageRequest.From(cursor, limit), cancellationToken));
        })
        .RequireAuthorization()
        .WithName("ListMyClaims")
        .Produces<Page<RequestView>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/requests/{id:guid}/claim", async (
            Guid id, HttpContext context, ClaimRequestHandler handler, CancellationToken cancellationToken) =>
        {
            var member = context.CurrentMember();

            return await handler.HandleAsync(new ClaimRequestCommand(id, member.MemberId), cancellationToken) is { } view
                ? Results.Ok(view)
                : Problems.RequestNotFound();
        })
        .RequireAuthorization()
        .WithName("ClaimRequest")
        .Produces<RequestView>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/api/requests/{id:guid}/cancel", async (
            Guid id, HttpContext context, CancelRequestHandler handler, CancellationToken cancellationToken) =>
        {
            var member = context.CurrentMember();

            return await handler.HandleAsync(new CancelRequestCommand(id, member.MemberId), cancellationToken) is { } view
                ? Results.Ok(view)
                : Problems.RequestNotFound();
        })
        .RequireAuthorization()
        .WithName("CancelRequest")
        .Produces<RequestView>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/api/requests/{id:guid}/withdraw", async (
            Guid id, HttpContext context, WithdrawClaimHandler handler, CancellationToken cancellationToken) =>
        {
            var member = context.CurrentMember();

            return await handler.HandleAsync(new WithdrawClaimCommand(id, member.MemberId), cancellationToken) is { } view
                ? Results.Ok(view)
                : Problems.RequestNotFound();
        })
        .RequireAuthorization()
        .WithName("WithdrawClaim")
        .Produces<RequestView>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/api/requests/quote", ([AsParameters] QuoteQuery query) =>
        {
            return Results.Ok(QuoteRequest.Handle(query.ToDetails()));
        })
        .RequireAuthorization()
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
        bool Lunch,
        bool Snack,
        bool Dinner)
    {
        public CreditRequestDetails ToDetails() =>
            new(DurationMinutes, ChildCount, Location, Transport, Meals(Lunch, Snack, Dinner));
    }

    private sealed record QuoteQuery(
        [FromQuery] int DurationMinutes,
        [FromQuery] int ChildCount,
        [FromQuery] BabysittingLocation Location = BabysittingLocation.SittersHouse,
        [FromQuery] TransportOption Transport = TransportOption.None,
        [FromQuery] bool Lunch = false,
        [FromQuery] bool Snack = false,
        [FromQuery] bool Dinner = false)
    {
        public CreditRequestDetails ToDetails() =>
            new(DurationMinutes, ChildCount, Location, Transport, Meals(Lunch, Snack, Dinner));
    }

    private static MealOption Meals(bool lunch, bool snack, bool dinner) =>
        (lunch ? MealOption.Lunch : MealOption.None) |
        (snack ? MealOption.Snack : MealOption.None) |
        (dinner ? MealOption.Dinner : MealOption.None);
}
