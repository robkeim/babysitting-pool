namespace BabysittingPool.Application.Flags;

/// <summary>
/// What an Admin can change about a flag at runtime (ADR 0016). A caller resolves in order: off for everyone when
/// not <see cref="Enabled"/>, off when denied, on when allowed, then the role's rollout percentage, then
/// <see cref="RolloutPercent"/>.
/// </summary>
public sealed record FeatureFlagSettings(
    bool Enabled,
    int RolloutPercent,
    IReadOnlyList<Guid> AllowedMemberIds,
    IReadOnlyList<Guid> DeniedMemberIds,
    IReadOnlyList<RoleRollout> RoleRollouts)
{
    public static readonly IReadOnlyList<string> Roles = ["Member", "Admin"];

    public static FeatureFlagSettings Initial(bool enabled) =>
        new(enabled, enabled ? 100 : 0, [], [], []);

    /// <summary>Callers who are not signed in have no id to hash, so they see only a flag that is on for everyone.</summary>
    public bool IsOnForAnonymous => Enabled && RolloutPercent == 100;

    /// <summary>Null when valid, otherwise the field that is wrong.</summary>
    public string? InvalidField()
    {
        if (RolloutPercent is < 0 or > 100)
        {
            return nameof(RolloutPercent);
        }

        return RoleRollouts.Any(r => !Roles.Contains(r.Role) || r.RolloutPercent is < 0 or > 100)
            ? nameof(RoleRollouts)
            : null;
    }
}

/// <summary>A role (Member or Admin) with its own rollout percentage.</summary>
public sealed record RoleRollout(string Role, int RolloutPercent);

/// <summary>The stored overrides, by flag name. A flag with no entry has not been seeded yet.</summary>
public interface IFeatureFlagStore
{
    Task<IReadOnlyDictionary<string, FeatureFlagSettings>> GetAllAsync(CancellationToken cancellationToken);

    Task SaveAsync(string name, FeatureFlagSettings settings, CancellationToken cancellationToken);

    /// <summary>Inserts a row for each flag that has none, from <paramref name="initial"/>; existing rows are kept.</summary>
    Task SeedAsync(IReadOnlyDictionary<string, bool> initial, CancellationToken cancellationToken);
}
