namespace BabysittingPool.Infrastructure.Persistence;

/// <summary>A flag's runtime settings (ADR 0016). The lists are JSON text: they are read and written whole.</summary>
public sealed class FeatureFlagRecord
{
    public string Name { get; set; } = "";

    public bool Enabled { get; set; }

    public int RolloutPercent { get; set; }

    public string AllowedMemberIds { get; set; } = "[]";

    public string DeniedMemberIds { get; set; } = "[]";

    public string RoleRollouts { get; set; } = "[]";
}
