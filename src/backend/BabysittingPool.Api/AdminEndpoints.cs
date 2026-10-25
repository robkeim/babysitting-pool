using BabysittingPool.Application;
using BabysittingPool.Application.Credits;
using BabysittingPool.Application.Flags;
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

        // Irreversible, and only for a closed Member; repeating it changes nothing (spec §2A, ADR 0014).
        group.MapPost("/members/{id:guid}/erase", async (
            Guid id, EraseMemberHandler handler, CancellationToken cancellationToken) =>
        {
            try
            {
                return await handler.HandleAsync(new EraseMemberCommand(id), cancellationToken)
                    ? Results.NoContent()
                    : Problems.MemberNotFound();
            }
            catch (DomainException ex)
            {
                return Problems.DomainRule(ex);
            }
        })
        .WithName("EraseMember")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/feature-flags", async (ListFeatureFlagsHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(new Page<FeatureFlagView>(await handler.HandleAsync(cancellationToken), null)))
        .WithName("ListFeatureFlags")
        .Produces<Page<FeatureFlagView>>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // Takes effect without a restart (ADR 0016). Replaces the flag's settings whole.
        group.MapPut("/feature-flags", async (
            UpdateFeatureFlagRequest request, UpdateFeatureFlagHandler handler, CancellationToken cancellationToken) =>
        {
            var settings = new FeatureFlagSettings(
                request.Enabled,
                request.RolloutPercent,
                request.AllowedMemberIds,
                request.DeniedMemberIds,
                request.RoleRollouts);
            if (settings.InvalidField() is { } field)
            {
                return Problems.InvalidRequest(char.ToLowerInvariant(field[0]) + field[1..], "out_of_range");
            }

            return await handler.HandleAsync(new UpdateFeatureFlagCommand(request.Name, settings), cancellationToken) is { } view
                ? Results.Ok(view)
                : Problems.FeatureFlagNotFound();
        })
        .WithName("UpdateFeatureFlag")
        .Produces<FeatureFlagView>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private sealed record UpdateFeatureFlagRequest(
        string Name,
        bool Enabled,
        int RolloutPercent,
        IReadOnlyList<Guid> AllowedMemberIds,
        IReadOnlyList<Guid> DeniedMemberIds,
        IReadOnlyList<RoleRollout> RoleRollouts);
}
