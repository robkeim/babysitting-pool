namespace BabysittingPool.Infrastructure.Persistence;

/// <summary>
/// An in-app Notification (ADR 0019): ids, kind, time and a small payload. No address, free text or name. Dismissing
/// deletes the row.
/// </summary>
public sealed class NotificationRecord
{
    public Guid Id { get; set; }

    /// <summary>The Member it is for.</summary>
    public Guid MemberId { get; set; }

    public string Kind { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public Guid RequestId { get; set; }

    /// <summary>Posted only: the Request's Area, if it has one.</summary>
    public string? Area { get; set; }

    /// <summary>Posted only.</summary>
    public DateTime? StartsAt { get; set; }

    /// <summary>Claimed only.</summary>
    public Guid? SitterId { get; set; }
}
