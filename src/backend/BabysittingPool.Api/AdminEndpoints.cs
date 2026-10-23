using BabysittingPool.Application.Requests;

namespace BabysittingPool.Api;

internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // The same use case as the hosted service, for the Admin to run on demand; it takes no timestamp (ADR 0006, 0007).
        app.MapPost("/api/admin/settle", async (
            HttpContext context, SettleHandler handler, CancellationToken cancellationToken) =>
        {
            if (await SessionCookie.CurrentMemberAsync(context, cancellationToken) is not { } member)
            {
                return Problems.Unauthenticated();
            }

            return member.IsAdmin
                ? Results.Ok(await handler.HandleAsync(cancellationToken))
                : Problems.NotAdmin();
        })
        .WithName("Settle")
        .Produces<SettleResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
