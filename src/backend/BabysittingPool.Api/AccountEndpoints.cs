using BabysittingPool.Application.Credits;

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

            try
            {
                var account = await handler.HandleAsync(new GetMyAccountQuery(member.MemberId, cursor, limit), cancellationToken);
                return account is null ? Problems.AccountNotFound() : Results.Ok(account);
            }
            catch (InvalidCursorException)
            {
                return Problems.InvalidRequest("cursor", "invalid");
            }
        })
        .RequireAuthorization()
        .WithName("GetMyAccount")
        .Produces<AccountView>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
