namespace BabysittingPool.Domain.Identity;

/// <summary>
/// Whether a Member sees the co-op light, dark, or as their device is set (GLOSSARY: Color Scheme). System is the
/// default and follows the browser or operating system setting.
/// </summary>
public sealed record ColorScheme
{
    public static readonly ColorScheme System = new("system");
    public static readonly ColorScheme Light = new("light");
    public static readonly ColorScheme Dark = new("dark");

    private static readonly ColorScheme[] All = [System, Light, Dark];

    private ColorScheme(string tag) => Tag = tag;

    public string Tag { get; }

    public static bool TryParse(string? tag, out ColorScheme colorScheme)
    {
        colorScheme = All.FirstOrDefault(c => c.Tag == tag)!;
        return colorScheme is not null;
    }

    /// <summary>A stored tag; one this version does not know falls back to System.</summary>
    public static ColorScheme Restore(string tag) => All.FirstOrDefault(c => c.Tag == tag) ?? System;

    public override string ToString() => Tag;
}
