using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Application.Flags;

public sealed record FeatureFlagView(
    string Name,
    bool DefaultEnabled,
    int? RemovalIssue,
    bool Enabled,
    int RolloutPercent,
    IReadOnlyList<Guid> AllowedMemberIds,
    IReadOnlyList<Guid> DeniedMemberIds,
    IReadOnlyList<RoleRollout> RoleRollouts)
{
    internal static FeatureFlagView From(FeatureFlag flag, FeatureFlagSettings? stored)
    {
        var settings = stored ?? FeatureFlagSettings.Initial(flag.Default);
        return new FeatureFlagView(
            flag.Name,
            flag.Default,
            flag.RemovalIssue,
            settings.Enabled,
            settings.RolloutPercent,
            settings.AllowedMemberIds,
            settings.DeniedMemberIds,
            settings.RoleRollouts);
    }
}

public sealed record UpdateFeatureFlagCommand(string Name, FeatureFlagSettings Settings);

public sealed class ListFeatureFlagsHandler(FeatureFlagRegistry registry, IFeatureFlagStore store)
{
    public async Task<IReadOnlyList<FeatureFlagView>> HandleAsync(CancellationToken cancellationToken)
    {
        var stored = await store.GetAllAsync(cancellationToken);
        return registry.All.Select(flag => FeatureFlagView.From(flag, stored.GetValueOrDefault(flag.Name))).ToList();
    }
}

public sealed class UpdateFeatureFlagHandler(FeatureFlagRegistry registry, IFeatureFlagStore store)
{
    /// <summary>Null when no such flag is declared. A bad value raises <see cref="DomainException"/>.</summary>
    public async Task<FeatureFlagView?> HandleAsync(UpdateFeatureFlagCommand command, CancellationToken cancellationToken)
    {
        if (registry.Find(command.Name) is not { } flag)
        {
            return null;
        }

        if (command.Settings.InvalidField() is { } field)
        {
            throw new DomainException($"{field} is out of range.", "invalid_feature_flag");
        }

        await store.SaveAsync(flag.Name, command.Settings, cancellationToken);
        return FeatureFlagView.From(flag, command.Settings);
    }
}
