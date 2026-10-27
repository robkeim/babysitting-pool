namespace BabysittingPool.Application.Flags;

/// <summary>A flag declared in code (ADR 0016): its name, the value it starts with, and the issue that removes it.</summary>
public sealed record FeatureFlag(string Name, bool Default, int RemovalIssue);

/// <summary>
/// The only place flags are defined. Adding a flag opens its removal issue and cites it here; the slice that
/// removes the flag deletes its entry, its code path and, by migration, its store row.
/// </summary>
public sealed class FeatureFlagRegistry
{
    private readonly Dictionary<string, FeatureFlag> _byName;

    public FeatureFlagRegistry(IEnumerable<FeatureFlag> flags)
    {
        All = flags.ToList();
        _byName = All.ToDictionary(f => f.Name, StringComparer.Ordinal);
    }

    /// <summary>The flags in production code.</summary>
    public static FeatureFlagRegistry Production { get; } = new(
    [
        // In-app Notifications (ADR 0019); removed by #160 once fully on in production.
        new FeatureFlag("notifications", false, 160),
    ]);

    public IReadOnlyList<FeatureFlag> All { get; }

    public FeatureFlag? Find(string name) => _byName.GetValueOrDefault(name);
}
