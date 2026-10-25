using BabysittingPool.Application.Flags;
using BabysittingPool.Application.Identity;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
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
        services.AddSingleton<FakeTimeProvider>(new SettableClock(InitialClock));
        services.AddSingleton<TimeProvider>(sp => sp.GetRequiredService<FakeTimeProvider>());
    }

    public static void MapTestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/test").AllowAnonymous().ExcludeFromDescription();

        // An endpoint gated on each declared flag, so tests can see a flag take effect (ADR 0016).
        foreach (var flag in app.ServiceProvider.GetRequiredService<FeatureFlagRegistry>().All)
        {
            group.MapGet($"/gated/{flag.Name}", () => Results.Ok()).RequireFeature(flag.Name);
        }

        group.MapPost("/members", async (SeedMemberRequest request, IServiceProvider services, CancellationToken cancellationToken) =>
        {
            if (request.GrantsQuarterCredits is not { Length: > 0 } || request.GrantsQuarterCredits.Any(g => g < 0))
            {
                return Problems.InvalidRequest("grantsQuarterCredits", "invalid");
            }

            var grants = request.GrantsQuarterCredits.Select(Credits.FromQuarterCredits).ToList();
            var email = request.Email ?? $"member-{Guid.NewGuid():N}@example.test";
            var memberId = await services.SeedMemberAsync(email, request.DisplayName ?? "Test Member", false, grants, cancellationToken);
            return Results.Created($"/api/test/members/{memberId}", new SeedMemberResponse(memberId, email));
        });

        // The sign-in shortcut for journeys that are not about signing in: a session secret for the browser's cookie.
        group.MapPost("/sessions", async (
            CreateSessionRequest request, IIdentityStore store, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var secret = Secrets.Generate();
            await store.AddSessionAsync(
                Session.Start(request.MemberId, Secrets.Hash(secret), clock.GetUtcNow().UtcDateTime), cancellationToken);
            return Results.Ok(new CreateSessionResponse(secret));
        });

        // The Admin endpoint is the same use case, but a second Admin would block the bootstrap Admin's Invitation.
        group.MapPost("/settle", async (SettleHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)));

        group.MapPut("/clock", (SetClockRequest request, FakeTimeProvider clock) =>
        {
            if (clock is SettableClock settable)
            {
                settable.Set(request.Now);
            }
            else
            {
                clock.SetUtcNow(request.Now);
            }

            return Results.NoContent();
        });
    }

    /// <summary>
    /// A fake clock that can also be set back. A worker's backend outlives its tests, and each test sets an absolute
    /// time at its start, which <see cref="FakeTimeProvider.SetUtcNow"/> refuses when it is earlier than the last test's.
    /// </summary>
    private sealed class SettableClock(DateTimeOffset start) : FakeTimeProvider(start)
    {
        private TimeSpan _shift;

        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + _shift;

        public void Set(DateTimeOffset now) => _shift = now - base.GetUtcNow();
    }

    private sealed record SeedMemberRequest(long[]? GrantsQuarterCredits, string? Email, string? DisplayName);

    private sealed record SeedMemberResponse(Guid MemberId, string Email);

    private sealed record CreateSessionRequest(Guid MemberId);

    private sealed record CreateSessionResponse(string Token);

    private sealed record SetClockRequest(DateTimeOffset Now);
}
