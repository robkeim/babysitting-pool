using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Domain.Identity;

/// <summary>A person in the co-op (credit-engine.md §2A). A closed Member keeps their history but cannot sign in.</summary>
public sealed class Member
{
    private Member(Guid id, EmailAddress email, DisplayName displayName, Language language, ColorScheme colorScheme, bool isAdmin, DateTime createdAt, DateTime? closedAt, HomeLocation? home)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        Language = language;
        ColorScheme = colorScheme;
        IsAdmin = isAdmin;
        CreatedAt = createdAt;
        ClosedAt = closedAt;
        Home = home;
    }

    public Guid Id { get; }

    public EmailAddress Email { get; private set; }

    public DisplayName DisplayName { get; private set; }

    public Language Language { get; }

    public ColorScheme ColorScheme { get; }

    public bool IsAdmin { get; }

    public DateTime CreatedAt { get; }

    public DateTime? ClosedAt { get; private set; }

    /// <summary>Their area and exact address, kept once on the profile and copied onto a Request posted at their house (spec §2A, §4.1).</summary>
    public HomeLocation? Home { get; private set; }

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

    /// <summary>What Erasure leaves in place of a name and an email address (spec §2A, ADR 0014).</summary>
    public const string Tombstone = "Former member";

    /// <summary>
    /// Replaces the personal details of a closed Member with <see cref="Tombstone"/> (spec §2A, ADR 0014). Irreversible,
    /// and safe to repeat. An open Member, the Admin included, is refused.
    /// </summary>
    public void Erase()
    {
        if (!IsClosed)
        {
            throw new DomainException("Only a closed Member can be erased.", "member_not_closed");
        }

        Email = EmailAddress.Restore(Tombstone);
        DisplayName = DisplayName.Restore(Tombstone);
        Home = null;
    }

    /// <summary>Sets the area and address on the profile (spec §2A). A closed Member's profile is frozen.</summary>
    public void MoveTo(HomeLocation home)
    {
        ArgumentNullException.ThrowIfNull(home);
        if (IsClosed)
        {
            throw new DomainException("A closed Member's profile cannot be changed.", "invalid_state");
        }

        Home = home;
    }

    public static Member Join(EmailAddress email, DisplayName displayName, bool isAdmin, DateTime now) =>
        new(Guid.NewGuid(), email, displayName, Language.EnglishUs, ColorScheme.System, isAdmin, now, null, null);

    public static Member Restore(
        Guid id, EmailAddress email, DisplayName displayName, Language language, ColorScheme colorScheme, bool isAdmin, DateTime createdAt,
        DateTime? closedAt, HomeLocation? home = null) =>
        new(id, email, displayName, language, colorScheme, isAdmin, createdAt, closedAt, home);
}
