namespace BabysittingPool.Application.Identity;

/// <summary>Co-op rules that govern sign-in (ADR 0011). Changing a rule is an edit here and a redeploy.</summary>
public sealed record CoopSettings
{
    public TimeSpan InvitationLifetime { get; init; } = TimeSpan.FromDays(7);
    public TimeSpan SignInLinkLifetime { get; init; } = TimeSpan.FromDays(1);

    /// <summary>A session expires after this long without use.</summary>
    public TimeSpan SessionIdleLifetime { get; init; } = TimeSpan.FromDays(30);

    /// <summary>A session's last-use time is rewritten only when it is older than this, so reads rarely write.</summary>
    public TimeSpan SessionTouchInterval { get; init; } = TimeSpan.FromHours(1);
}
