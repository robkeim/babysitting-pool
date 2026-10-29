namespace BabysittingPool.Application.Flags;

/// <summary>
/// A flag declared in code (ADR 0016): its name, the value it starts with, and the issue that removes it. A
/// <c>null</c> removal issue declares an operator toggle, which stays for good.
/// </summary>
public sealed record FeatureFlag(string Name, bool Default, int? RemovalIssue);

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
        // Operator toggle: the co-op's vacation look, flipped from the admin screen (ADR 0016 amendment 2026-10-29).
        new FeatureFlag("vacationMode", false, null),
    ]);

    public IReadOnlyList<FeatureFlag> All { get; }

    public FeatureFlag? Find(string name) => _byName.GetValueOrDefault(name);
}
