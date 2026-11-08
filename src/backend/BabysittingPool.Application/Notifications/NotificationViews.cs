namespace BabysittingPool.Application.Notifications;

/// <summary>The four kinds of in-app Notification (spec §2A, ADR 0019). The value is what the API and the table carry.</summary>
public static class NotificationKinds
{
    /// <summary>A Request was Posted; goes to every open Member but the Requester.</summary>
    public const string RequestPosted = "request_posted";

    /// <summary>A Request was Claimed; goes to its Requester.</summary>
    public const string RequestClaimed = "request_claimed";

    /// <summary>A Claimed Request was canceled by its Requester; goes to its Sitter.</summary>
    public const string RequestCanceled = "request_canceled";

    /// <summary>A Sitter withdrew their claim; goes to the Requester. The Request may be back to Posted or Expired.</summary>
    public const string RequestWithdrawn = "request_withdrawn";
}

/// <summary>
/// One Notification as its Member sees it: ids, kind, time and a small payload, never an address, a name or free
/// text. <see cref="Area"/> and <see cref="StartsAt"/> are set for a Posted Request (Area only if it has one),
/// and <see cref="StartsAt"/> for a Canceled or Withdrawn one;
/// <see cref="SitterId"/> for a Claimed one.
/// </summary>
public sealed record NotificationView(
    Guid Id,
    string Kind,
    DateTime CreatedAt,
    Guid RequestId,
    string? Area,
    DateTime? StartsAt,
    Guid? SitterId);
