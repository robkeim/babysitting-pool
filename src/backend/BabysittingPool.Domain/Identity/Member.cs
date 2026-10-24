using BabysittingPool.Domain.Credits;

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
    public DateTime? ClosedAt { get; private set; }

    public bool IsClosed => ClosedAt is not null;

    /// <summary>Closes the account on Offboarding (spec §2A). The single Admin cannot be offboarded.</summary>
    public void Close(DateTime now)
    {
        if (IsAdmin)
        {
            throw new DomainException("The Admin cannot be offboarded.", "cannot_offboard_admin");
        }

        if (IsClosed)
        {
            throw new DomainException("The Member is already closed.", "invalid_state");
        }

        ClosedAt = now;
    }

    public static Member Join(EmailAddress email, DisplayName displayName, bool isAdmin, DateTime now) =>
        new(Guid.NewGuid(), email, displayName, Language.EnglishUs, isAdmin, now, null);

    public static Member Restore(
        Guid id, EmailAddress email, DisplayName displayName, Language language, bool isAdmin, DateTime createdAt, DateTime? closedAt) =>
        new(id, email, displayName, language, isAdmin, createdAt, closedAt);
}
