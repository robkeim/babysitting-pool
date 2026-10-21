using BabysittingPool.Application.Credits;

namespace BabysittingPool.Api;

internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me/account", async (
            HttpContext context,
            IHostEnvironment environment,
            GetMyAccountHandler handler,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            if (DevIdentity.CurrentMemberId(context, environment) is not { } memberId)
                return Problems.Unauthenticated();

            try
            {
                var account = await handler.HandleAsync(new GetMyAccountQuery(memberId, cursor, limit), cancellationToken);
                return account is null ? Problems.AccountNotFound() : Results.Ok(account);
            }
            catch (InvalidCursorException)
            {
                return Problems.InvalidRequest("cursor", "invalid");
            }
        })
        .WithName("GetMyAccount")
        .Produces<AccountView>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
