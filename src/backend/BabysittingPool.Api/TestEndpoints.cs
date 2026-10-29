using System.Security.Cryptography;
using System.Text;
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
    public const string TokenSetting = "E2E:Token";
    public const string TokenHeader = "X-E2E-Token";

    private static readonly DateTimeOffset InitialClock = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>E2E runs on a clock the tests control.</summary>
    public static void AddTestClock(this IServiceCollection services)
    {
        services.AddSingleton<FakeTimeProvider>(new SettableClock(InitialClock));
        services.AddSingleton<TimeProvider>(sp => sp.GetRequiredService<FakeTimeProvider>());
    }

    /// <summary>
    /// Production must not carry anything that can change who sees what: it stops if the E2E token is configured or
    /// the host was pointed at the E2E environment through the other environment variable (ADR 0016).
    /// </summary>
    public static void RefuseE2EInProduction(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsProduction())
        {
            return;
        }

        if (!string.IsNullOrEmpty(builder.Configuration[TokenSetting]))
        {
            throw new InvalidOperationException($"{TokenSetting} must not be set in Production.");
        }

        foreach (var variable in new[] { "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT" })
        {
            if (string.Equals(System.Environment.GetEnvironmentVariable(variable), Environment, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{variable}={Environment} must not be present in Production.");
            }
        }
    }

    public static void MapTestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/test").AllowAnonymous().ExcludeFromDescription();

        // An endpoint gated on each declared flag, so tests can see a flag take effect (ADR 0016).
        foreach (var flag in app.ServiceProvider.GetRequiredService<FeatureFlagRegistry>().All)
        {
            group.MapGet($"/gated/{flag.Name}", () => Results.Ok()).RequireFeature(flag.Name);
        }

        // Changes who sees what, so unlike the endpoints below it needs the token; without it the answer is 404.
        group.MapPost("/feature-flags", async (
            SetFeatureFlagRequest request, UpdateFeatureFlagHandler handler, CancellationToken cancellationToken) =>
        {
            var settings = new FeatureFlagSettings(
                request.Enabled,
                request.RolloutPercent ?? (request.Enabled ? 100 : 0),
                request.AllowedMemberIds ?? [],
                request.DeniedMemberIds ?? [],
                request.RoleRollouts ?? []);
            if (settings.InvalidField() is { } field)
            {
                return Problems.InvalidRequest(char.ToLowerInvariant(field[0]) + field[1..], "out_of_range");
            }

            return await handler.HandleAsync(new UpdateFeatureFlagCommand(request.Name, settings), cancellationToken) is { } view
                ? Results.Ok(view)
                : Problems.FeatureFlagNotFound();
        })
        .AddEndpointFilter(async (context, next) =>
            HasToken(context.HttpContext) ? await next(context) : Results.NotFound());

        group.MapPost("/members", async (SeedMemberRequest request, IServiceProvider services, CancellationToken cancellationToken) =>
        {
            if (request.GrantsQuarterCredits is not { Length: > 0 } || request.GrantsQuarterCredits.Any(g => g < 0))
            {
                return Problems.InvalidRequest("grantsQuarterCredits", "invalid");
            }

            var grants = request.GrantsQuarterCredits.Select(Credits.FromQuarterCredits).ToList();
            var email = request.Email ?? $"member-{Guid.NewGuid():N}@example.test";
            var home = request.Area is null || request.Address is null ? null : HomeLocation.Create(request.Address, request.Area, "80021", "80", "80000", null);
            var memberId = await services.SeedMemberAsync(
                email, request.DisplayName ?? "Test Member", false, grants, home, cancellationToken);
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

    private static bool HasToken(HttpContext context)
    {
        var expected = context.RequestServices.GetRequiredService<IConfiguration>()[TokenSetting];
        var supplied = context.Request.Headers[TokenHeader].ToString();
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        // Hashing first gives both sides the same length, which FixedTimeEquals needs and which hides the token's.
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
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

    private sealed record SetFeatureFlagRequest(
        string Name,
        bool Enabled,
        int? RolloutPercent,
        List<Guid>? AllowedMemberIds,
        List<Guid>? DeniedMemberIds,
        List<RoleRollout>? RoleRollouts);

    private sealed record SeedMemberRequest(
        long[]? GrantsQuarterCredits, string? Email, string? DisplayName, string? Area, string? Address);

    private sealed record SeedMemberResponse(Guid MemberId, string Email);

    private sealed record CreateSessionRequest(Guid MemberId);

    private sealed record CreateSessionResponse(string Token);

    private sealed record SetClockRequest(DateTimeOffset Now);
}
