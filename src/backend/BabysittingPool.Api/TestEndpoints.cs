using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace BabysittingPool.Api;

/// <summary>
/// Test-only endpoints for Playwright (ADR 0004). Mapped only in the <c>E2E</c> environment, so they do not
/// exist in Production, and kept out of the OpenAPI document because they are not part of the contract.
/// </summary>
internal static class TestEndpoints
{
    public const string Environment = "E2E";

    private static readonly DateTimeOffset InitialClock = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>E2E runs on a clock the tests control.</summary>
    public static void AddTestClock(this IServiceCollection services)
    {
        services.AddSingleton(new FakeTimeProvider(InitialClock));
        services.AddSingleton<TimeProvider>(sp => sp.GetRequiredService<FakeTimeProvider>());
    }

    public static void MapTestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/test").ExcludeFromDescription();

        group.MapPost("/members", async (SeedMemberRequest request, IServiceProvider services, CancellationToken cancellationToken) =>
        {
            if (request.GrantsQuarterCredits is not { Length: > 0 } || request.GrantsQuarterCredits.Any(g => g < 0))
                return Problems.InvalidRequest("grantsQuarterCredits", "invalid");

            var grants = request.GrantsQuarterCredits.Select(Credits.FromQuarterCredits).ToList();
            var memberId = await services.SeedMemberWithGrantsAsync(grants, cancellationToken);
            return Results.Created($"/api/test/members/{memberId}", new SeedMemberResponse(memberId));
        });

        group.MapPut("/clock", (SetClockRequest request, FakeTimeProvider clock) =>
        {
            clock.SetUtcNow(request.Now);
            return Results.NoContent();
        });
    }

    private sealed record SeedMemberRequest(long[]? GrantsQuarterCredits);

    private sealed record SeedMemberResponse(Guid MemberId);

    private sealed record SetClockRequest(DateTimeOffset Now);
}
