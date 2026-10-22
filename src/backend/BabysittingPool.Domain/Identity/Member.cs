namespace BabysittingPool.Domain.Identity;

/// <summary>A person in the co-op (credit-engine.md §2A). A closed Member keeps their history but cannot sign in.</summary>
public sealed class Member
{
    private Member(Guid id, EmailAddress email, DisplayName displayName, Language language, bool isAdmin, DateTime createdAt, DateTime? closedAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        Language = language;
        IsAdmin = isAdmin;
        CreatedAt = createdAt;
        ClosedAt = closedAt;
    }

    public Guid Id { get; }
    public EmailAddress Email { get; }
    public DisplayName DisplayName { get; }
    public Language Language { get; }
    public bool IsAdmin { get; }
    public DateTime CreatedAt { get; }
    public DateTime? ClosedAt { get; }

    public bool IsClosed => ClosedAt is not null;

    public static Member Join(EmailAddress email, DisplayName displayName, bool isAdmin, DateTime now) =>
        new(Guid.NewGuid(), email, displayName, Language.EnglishUs, isAdmin, now, null);

    public static Member Restore(
        Guid id, EmailAddress email, DisplayName displayName, Language language, bool isAdmin, DateTime createdAt, DateTime? closedAt) =>
        new(id, email, displayName, language, isAdmin, createdAt, closedAt);
}
