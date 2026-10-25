namespace BabysittingPool.Domain.Identity;

/// <summary>A single-use, short-lived link that signs a Member in. Only the hash of the secret is kept.</summary>
public sealed class SignInLink
{
    private SignInLink(Guid id, Guid memberId, string secretHash, DateTime expiresAt, DateTime? consumedAt)
    {
        Id = id;
        MemberId = memberId;
        SecretHash = secretHash;
        ExpiresAt = expiresAt;
        ConsumedAt = consumedAt;
    }

    public Guid Id { get; }

    public Guid MemberId { get; }

    public string SecretHash { get; }

    public DateTime ExpiresAt { get; }

    public DateTime? ConsumedAt { get; }

    public static SignInLink Issue(Guid memberId, string secretHash, DateTime now, TimeSpan lifetime) =>
        new(Guid.NewGuid(), memberId, secretHash, now + lifetime, null);

    public static SignInLink Restore(Guid id, Guid memberId, string secretHash, DateTime expiresAt, DateTime? consumedAt) =>
        new(id, memberId, secretHash, expiresAt, consumedAt);

    public bool IsUsableAt(DateTime now) => ConsumedAt is null && now < ExpiresAt;
}
