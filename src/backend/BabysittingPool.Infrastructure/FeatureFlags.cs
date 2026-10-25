using System.Globalization;
using System.Text.Json;
using BabysittingPool.Application.Flags;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;

namespace BabysittingPool.Infrastructure;

/// <summary>Flag settings read from SQLite behind a short cache that every write drops (ADR 0016).</summary>
internal sealed class FeatureFlagCache(TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private IReadOnlyDictionary<string, FeatureFlagSettings>? _snapshot;
    private DateTimeOffset _loadedAt;

    public bool TryGet(out IReadOnlyDictionary<string, FeatureFlagSettings> snapshot)
    {
        lock (_gate)
        {
            snapshot = _snapshot!;
            return _snapshot is not null && clock.GetUtcNow() - _loadedAt < Lifetime;
        }
    }

    public void Set(IReadOnlyDictionary<string, FeatureFlagSettings> snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
            _loadedAt = clock.GetUtcNow();
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _snapshot = null;
        }
    }
}

internal sealed class FeatureFlagStore(BabysittingPoolDbContext db, FeatureFlagCache cache) : IFeatureFlagStore
{
    public async Task<IReadOnlyDictionary<string, FeatureFlagSettings>> GetAllAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGet(out var cached))
        {
            return cached;
        }

        var records = await db.FeatureFlags.AsNoTracking().ToListAsync(cancellationToken);
        var snapshot = records.ToDictionary(r => r.Name, ToSettings, StringComparer.Ordinal);
        cache.Set(snapshot);
        return snapshot;
    }

    public async Task SaveAsync(string name, FeatureFlagSettings settings, CancellationToken cancellationToken)
    {
        var record = await db.FeatureFlags.SingleOrDefaultAsync(r => r.Name == name, cancellationToken);
        if (record is null)
        {
            record = new FeatureFlagRecord { Name = name };
            db.FeatureFlags.Add(record);
        }

        Apply(record, settings);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            cache.Invalidate();
        }
    }

    public async Task SeedAsync(IReadOnlyDictionary<string, bool> initial, CancellationToken cancellationToken)
    {
        var existing = await db.FeatureFlags.Select(r => r.Name).ToListAsync(cancellationToken);
        foreach (var (name, enabled) in initial.Where(i => !existing.Contains(i.Key)))
        {
            var record = new FeatureFlagRecord { Name = name };
            Apply(record, FeatureFlagSettings.Initial(enabled));
            db.FeatureFlags.Add(record);
        }

        await db.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
    }

    private static void Apply(FeatureFlagRecord record, FeatureFlagSettings settings)
    {
        record.Enabled = settings.Enabled;
        record.RolloutPercent = settings.RolloutPercent;
        record.AllowedMemberIds = JsonSerializer.Serialize(settings.AllowedMemberIds);
        record.DeniedMemberIds = JsonSerializer.Serialize(settings.DeniedMemberIds);
        record.RoleRollouts = JsonSerializer.Serialize(settings.RoleRollouts);
    }

    private static FeatureFlagSettings ToSettings(FeatureFlagRecord record) => new(
        record.Enabled,
        record.RolloutPercent,
        JsonSerializer.Deserialize<List<Guid>>(record.AllowedMemberIds) ?? [],
        JsonSerializer.Deserialize<List<Guid>>(record.DeniedMemberIds) ?? [],
        JsonSerializer.Deserialize<List<RoleRollout>>(record.RoleRollouts) ?? []);
}

/// <summary>
/// Feeds the stored settings to <c>Microsoft.FeatureManagement</c> as Targeting filter audiences, so the library
/// does the evaluation: exclusion first, then named users, then role groups, then the default rollout (ADR 0016).
/// </summary>
internal sealed class StoreFeatureDefinitionProvider(IServiceScopeFactory scopes, FeatureFlagRegistry registry)
    : IFeatureDefinitionProvider
{
    public async Task<FeatureDefinition> GetFeatureDefinitionAsync(string featureName)
    {
        var all = await LoadAsync();
        return all.FirstOrDefault(d => d.Name == featureName) ?? new FeatureDefinition { Name = featureName, EnabledFor = [] };
    }

    public async IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync()
    {
        foreach (var definition in await LoadAsync())
        {
            yield return definition;
        }
    }

    private async Task<IReadOnlyList<FeatureDefinition>> LoadAsync()
    {
        await using var scope = scopes.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IFeatureFlagStore>().GetAllAsync(CancellationToken.None);
        return registry.All
            .Select(flag => Define(flag.Name, stored.GetValueOrDefault(flag.Name) ?? FeatureFlagSettings.Initial(flag.Default)))
            .ToList();
    }

    private static FeatureDefinition Define(string name, FeatureFlagSettings settings)
    {
        if (!settings.Enabled)
        {
            return new FeatureDefinition { Name = name, EnabledFor = [] };
        }

        var values = new Dictionary<string, string?>
        {
            ["Audience:DefaultRolloutPercentage"] = settings.RolloutPercent.ToString(CultureInfo.InvariantCulture),
        };
        for (var i = 0; i < settings.AllowedMemberIds.Count; i++)
        {
            values[$"Audience:Users:{i}"] = settings.AllowedMemberIds[i].ToString();
        }

        for (var i = 0; i < settings.DeniedMemberIds.Count; i++)
        {
            values[$"Audience:Exclusion:Users:{i}"] = settings.DeniedMemberIds[i].ToString();
        }

        var group = 0;
        foreach (var (role, percent) in settings.RoleRollouts)
        {
            values[$"Audience:Groups:{group}:Name"] = role;
            values[$"Audience:Groups:{group}:RolloutPercentage"] = percent.ToString(CultureInfo.InvariantCulture);
            group++;
        }

        var parameters = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new FeatureDefinition
        {
            Name = name,
            EnabledFor = [new FeatureFilterConfiguration { Name = "Targeting", Parameters = parameters }],
        };
    }
}

public static class FeatureFlagServices
{
    /// <summary>Registers the store, and the definition provider in place of the library's configuration-backed one.</summary>
    public static IServiceCollection AddFeatureFlagStore(this IServiceCollection services)
    {
        services.AddSingleton<FeatureFlagCache>();
        services.AddScoped<IFeatureFlagStore, FeatureFlagStore>();
        services.Replace(ServiceDescriptor.Singleton<IFeatureDefinitionProvider, StoreFeatureDefinitionProvider>());
        return services;
    }

    /// <summary>
    /// Writes a row for every declared flag that has none. <c>Features__&lt;Name&gt;</c> sets a flag's first value and is
    /// ignored once the flag has a row; a name in <c>Features</c> that is not declared throws.
    /// </summary>
    public static async Task SeedFeatureFlagsAsync(
        this IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var registry = services.GetRequiredService<FeatureFlagRegistry>();
        var configured = configuration.GetSection("Features").GetChildren().ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);
        var unknown = configured.Keys.Where(name => registry.Find(name) is null).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException($"Features:{unknown[0]} is not a declared flag.");
        }

        var initial = registry.All.ToDictionary(
            flag => flag.Name,
            flag => configured.TryGetValue(flag.Name, out var value) && bool.TryParse(value, out var parsed) ? parsed : flag.Default);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IFeatureFlagStore>().SeedAsync(initial, cancellationToken);
    }
}
