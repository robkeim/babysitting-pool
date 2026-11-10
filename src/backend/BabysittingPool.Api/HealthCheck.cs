using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Api;

/// <summary>
/// Fly's liveness probe (ADR 0018): 200 when the main database answers, 503 when it does not. Anonymous and without
/// details in the body, because anyone can call it (ADR 0014).
/// </summary>
internal static class HealthCheck
{
    public static async Task<IResult> HandleAsync(BabysittingPoolDbContext db, ILogger<BabysittingPoolDbContext> log, CancellationToken ct)
    {
        try
        {
            // Reads a real table, so a missing or corrupt file fails here; a bare SELECT 1 never touches the file.
            await db.Members.AsNoTracking().AnyAsync(ct);
            return Results.Ok(new { status = "ok" });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Health check: the database did not answer");
            return Results.Json(new { status = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
