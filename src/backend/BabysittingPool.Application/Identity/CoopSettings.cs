namespace BabysittingPool.Application.Identity;

/// <summary>Co-op rules that govern sign-in and Requests (ADR 0011). Changing a rule is an edit here and a redeploy.</summary>
public sealed record CoopSettings
{
    public TimeSpan InvitationLifetime { get; init; } = TimeSpan.FromDays(7);
    public TimeSpan SignInLinkLifetime { get; init; } = TimeSpan.FromDays(1);

    /// <summary>A session expires after this long without use.</summary>
    public TimeSpan SessionIdleLifetime { get; init; } = TimeSpan.FromDays(30);

    /// <summary>A session's last-use time is rewritten only when it is older than this, so reads rarely write.</summary>
    public TimeSpan SessionTouchInterval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>The least notice a Request must give between posting and its start time (spec §4.1).</summary>
    public TimeSpan MinimumLeadTime { get; init; } = TimeSpan.FromHours(6);

    /// <summary>A Posted Request can no longer be claimed this long before its start time (spec §4.2).</summary>
    public TimeSpan ClaimCutoff { get; init; } = TimeSpan.FromHours(3);
}
