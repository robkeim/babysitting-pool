namespace BabysittingPool.Infrastructure.Persistence;

public sealed class MemberRecord
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public sealed class InvitationRecord
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public bool IsAdmin { get; set; }
    public string SecretHash { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }

    /// <summary>Optimistic concurrency token: two people submitting one Invitation cannot both win (ADR 0004).</summary>
    public int Version { get; set; }
}

public sealed class SignInLinkRecord
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string SecretHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }

    /// <summary>Optimistic concurrency token, as on <see cref="InvitationRecord"/>.</summary>
    public int Version { get; set; }
}

public sealed class SessionRecord
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string SecretHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
}
