using BabysittingPool.Application;
using BabysittingPool.Application.Addresses;
using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Api;

internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me/account", async (
            HttpContext context,
            GetMyAccountHandler handler,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var member = context.CurrentMember();

            var account = await handler.HandleAsync(new GetMyAccountQuery(member.MemberId, cursor, limit), cancellationToken);
            return account is null ? Problems.AccountNotFound() : Results.Ok(account);
        })
        .RequireAuthorization()
        .WithName("GetMyAccount")
        .Produces<AccountView>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/me/profile", async (
            HttpContext context, GetMyProfileHandler handler, CancellationToken cancellationToken) =>
            await handler.HandleAsync(context.CurrentMember().MemberId, cancellationToken) is { } profile
                ? Results.Ok(profile)
                : Problems.MemberNotFound())
        .RequireAuthorization()
        .WithName("GetMyProfile")
        .Produces<ProfileView>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("/api/me/profile", async (
            HttpContext context, ProfileBody body, UpdateMyProfileHandler handler, CancellationToken cancellationToken) =>
        {
            var command = new UpdateMyProfileCommand(context.CurrentMember().MemberId, body.BanId, body.Label, body.Instructions);
            return await handler.HandleAsync(command, cancellationToken) is { } profile
                ? Results.Ok(profile)
                : Problems.MemberNotFound();
        })
        .RequireAuthorization()
        .WithName("UpdateMyProfile")
        .Produces<ProfileView>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/rules", (GetRulesHandler handler) => Results.Ok(handler.Handle()))
        .RequireAuthorization()
        .WithName("GetRules")
        .Produces<RulesView>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private sealed record ProfileBody(string? BanId, string? Label, string? Instructions);
}
