using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Domain.Identity;

/// <summary>
/// A single-use link that lets one person create their account (credit-engine.md §2A). Only the hash of the
/// secret is kept.
/// </summary>
public sealed class Invitation
{
    private Invitation(
        Guid id, EmailAddress email, bool isAdmin, string secretHash, DateTime issuedAt, DateTime expiresAt, DateTime? consumedAt)
    {
        Id = id;
        Email = email;
        IsAdmin = isAdmin;
        SecretHash = secretHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        ConsumedAt = consumedAt;
    }

    public Guid Id { get; }

    public EmailAddress Email { get; }

    public bool IsAdmin { get; }

    public string SecretHash { get; }

    public DateTime IssuedAt { get; }

    public DateTime ExpiresAt { get; }

    public DateTime? ConsumedAt { get; }

    public static Invitation Issue(EmailAddress email, bool isAdmin, string secretHash, DateTime now, TimeSpan lifetime) =>
        new(Guid.NewGuid(), email, isAdmin, secretHash, now, now + lifetime, null);

    public static Invitation Restore(
        Guid id, EmailAddress email, bool isAdmin, string secretHash, DateTime issuedAt, DateTime expiresAt, DateTime? consumedAt) =>
        new(id, email, isAdmin, secretHash, issuedAt, expiresAt, consumedAt);

    public bool IsUsableAt(DateTime now) => ConsumedAt is null && now < ExpiresAt;

    public InvitationStatus StatusAt(DateTime now) =>
        ConsumedAt is not null ? InvitationStatus.Accepted : now < ExpiresAt ? InvitationStatus.Pending : InvitationStatus.Expired;

    /// <summary>
    /// The same Invitation with a fresh secret and validity, for the Admin to re-send. The old link stops working.
    /// Throws if the Invitation was already accepted.
    /// </summary>
    public Invitation Reissue(string secretHash, DateTime now, TimeSpan lifetime) =>
        ConsumedAt is null
            ? new(Id, Email, IsAdmin, secretHash, now, now + lifetime, null)
            : throw new DomainException("The Invitation was already accepted.", "invitation_already_accepted");
}

public enum InvitationStatus
{
    Pending,
    Accepted,
    Expired,
}
