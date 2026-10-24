using BabysittingPool.Application.Offboarding;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;

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

        // Closes the account, releases the Member's open Requests and burns their balance; repeating it finishes
        // anything an earlier failed run left (spec §2A).
        group.MapPost("/members/{id:guid}/offboard", async (
            Guid id, OffboardMemberHandler handler, CancellationToken cancellationToken) =>
        {
            try
            {
                return await handler.HandleAsync(new OffboardMemberCommand(id), cancellationToken) is { } result
                    ? Results.Ok(result)
                    : Problems.MemberNotFound();
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
        .WithName("OffboardMember")
        .Produces<OffboardMemberResult>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
