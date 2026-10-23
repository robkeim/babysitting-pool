using BabysittingPool.Application.Requests;

namespace BabysittingPool.Api;

internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // One group policy, so a new Admin endpoint cannot forget it; a non-Admin gets 403 (ADR 0006, 0014).
        var group = app.MapGroup("/api/admin").RequireAuthorization(Authorization.AdminPolicy);

        // The same use case as the hosted service, for the Admin to run on demand; it takes no timestamp (ADR 0006, 0007).
        group.MapPost("/settle", async (SettleHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)))
        .WithName("Settle")
        .Produces<SettleResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
