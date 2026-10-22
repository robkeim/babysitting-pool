namespace BabysittingPool.Domain.Identity;

/// <summary>
/// The language a Member reads the co-op in, as a BCP 47 tag (GLOSSARY: Language). Ünïcödé Tëst (en-XA) is a test
/// aid and is never offered in production.
/// </summary>
public sealed record Language
{
    public static readonly Language EnglishUs = new("en-US", isTest: false);
    public static readonly Language UnicodeTest = new("en-XA", isTest: true);

    private static readonly Language[] All = [EnglishUs, UnicodeTest];

    private Language(string tag, bool isTest)
    {
        Tag = tag;
        IsTest = isTest;
    }

    public string Tag { get; }
    public bool IsTest { get; }

    /// <summary>Parses a tag; test Languages are accepted only when <paramref name="includeTest"/> is set.</summary>
    public static bool TryParse(string? tag, bool includeTest, out Language language)
    {
        language = All.FirstOrDefault(l => l.Tag == tag && (includeTest || !l.IsTest))!;
        return language is not null;
    }

    /// <summary>A stored tag; a Language retired since it was chosen falls back to English.</summary>
    public static Language Restore(string tag) => All.FirstOrDefault(l => l.Tag == tag) ?? EnglishUs;

    public override string ToString() => Tag;
}
