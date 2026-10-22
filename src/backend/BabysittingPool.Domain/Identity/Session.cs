namespace BabysittingPool.Domain.Identity;

/// <summary>
/// A signed-in browser. It is valid until the idle lifetime has passed since it was last used, so using it
/// keeps it alive. Only the hash of the cookie secret is kept.
/// </summary>
public sealed class Session
{
    private Session(Guid id, Guid memberId, string secretHash, DateTime createdAt, DateTime lastUsedAt)
    {
        Id = id;
        MemberId = memberId;
        SecretHash = secretHash;
        CreatedAt = createdAt;
        LastUsedAt = lastUsedAt;
    }

    public Guid Id { get; }
    public Guid MemberId { get; }
    public string SecretHash { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastUsedAt { get; }

    public static Session Start(Guid memberId, string secretHash, DateTime now) =>
        new(Guid.NewGuid(), memberId, secretHash, now, now);

    public static Session Restore(Guid id, Guid memberId, string secretHash, DateTime createdAt, DateTime lastUsedAt) =>
        new(id, memberId, secretHash, createdAt, lastUsedAt);

    public bool IsActiveAt(DateTime now, TimeSpan idleLifetime) => now < LastUsedAt + idleLifetime;
}
