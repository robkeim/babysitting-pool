using System.Security.Claims;
using BabysittingPool.Application.Flags;
using BabysittingPool.Infrastructure;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace BabysittingPool.Api;

/// <summary>Who is asking, for the Targeting filter: the Member's id and their role as the one group (ADR 0016).</summary>
internal sealed class MemberTargetingContextAccessor(IHttpContextAccessor http) : ITargetingContextAccessor
{
    public ValueTask<TargetingContext> GetContextAsync()
    {
        var user = http.HttpContext?.User;
        var id = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = user?.FindFirstValue(ClaimTypes.Role);
        return ValueTask.FromResult(new TargetingContext { UserId = id, Groups = role is null ? [] : [role] });
    }
}

/// <summary>Answers whether a flag is on for the current caller.</summary>
internal sealed class FeatureFlagEvaluator(
    IFeatureManager manager, IFeatureFlagStore store, IHttpContextAccessor http)
{
    public async Task<bool> IsEnabledAsync(string name, CancellationToken cancellationToken)
    {
        if (http.HttpContext?.User.Identity?.IsAuthenticated == true)
        {
            return await manager.IsEnabledAsync(name, cancellationToken);
        }

        // No id to hash: only a flag that is fully on counts (ADR 0016).
        var stored = await store.GetAllAsync(cancellationToken);
        return stored.TryGetValue(name, out var settings) && settings.IsOnForAnonymous;
    }
}

internal static class FeatureFlagEndpoints
{
    public static void AddFeatureFlags(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddFeatureManagement().WithTargeting<MemberTargetingContextAccessor>();
        services.AddFeatureFlagStore();
        services.AddScoped<FeatureFlagEvaluator>();
    }

}

public static class FeatureGate
{
    /// <summary>The endpoint answers 404 while the flag is off for the caller (ADR 0016).</summary>
    public static TBuilder RequireFeature<TBuilder>(this TBuilder builder, string name)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var evaluator = context.HttpContext.RequestServices.GetRequiredService<FeatureFlagEvaluator>();
            return await evaluator.IsEnabledAsync(name, context.HttpContext.RequestAborted)
                ? await next(context)
                : Problems.FeatureOff();
        });
        return builder;
    }
}
