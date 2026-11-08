using BabysittingPool.Domain.Requests;

namespace BabysittingPool.Application.Admin;

/// <summary>Read port for the Admin's read-only views of the Members and the pool (spec §2A, §5).</summary>
public interface IAdminReader
{
    /// <summary>The Member and their balances, or null when there is no such Member.</summary>
    Task<MemberSummaryView?> FindMemberAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>The Requests the Member posted or claimed, soonest-starting last, as of <paramref name="now"/>.</summary>
    Task<Page<MemberRequestView>> ListMemberRequestsAsync(
        Guid memberId, DateTime now, PageRequest page, CancellationToken cancellationToken);

    Task<PoolOverview> GetPoolOverviewAsync(DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Open, non-Admin Members whose last activity (the start of their last Completed Request in either role, else
    /// their account creation) is before <paramref name="activeSince"/>, longest dormant first.
    /// </summary>
    Task<IReadOnlyList<DormantMemberView>> ListDormantAsync(DateTime activeSince, CancellationToken cancellationToken);
}

public sealed record MemberSummaryView(
    Guid Id,
    string DisplayName,
    string Email,
    bool IsAdmin,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    long AvailableQuarterCredits,
    long EscrowedQuarterCredits);

/// <summary>A Request seen from one Member's side. No address: the Admin needs the situation, not the house.</summary>
public sealed record MemberRequestView(
    Guid Id,
    MemberRequestRole Role,
    DateTime StartsAt,
    int DurationMinutes,
    int ChildCount,
    string? Area,
    long CostQuarterCredits,
    RequestStatus Status);

public enum MemberRequestRole
{
    Requester,
    Sitter,
}

/// <summary>Totals for the whole pool. Burned Credits are split by cause so neither hides the other.</summary>
public sealed record PoolOverview(
    long CreditsInCirculationQuarterCredits,
    long EscrowedQuarterCredits,
    long BurnedByDemurrageQuarterCredits,
    long BurnedByOffboardingQuarterCredits,
    int OpenMembers,
    RequestCounts Requests);

public sealed record RequestCounts(int Posted, int Claimed, int Completed, int Canceled, int Expired);

public sealed record DormantMemberView(
    Guid Id, string DisplayName, string Email, DateTime LastActivityAt, long AvailableQuarterCredits);
