using BabysittingPool.Application.Credits;

namespace BabysittingPool.Application.Admin;

/// <summary>A Member's balances, Requests and Ledger, as the Admin sees them (read-only, spec §2A).</summary>
public sealed record MemberDetailView(MemberSummaryView Member, Page<LedgerEntryView> Ledger);

public sealed class GetMemberDetailHandler(IAdminReader members, ICreditAccountReader accounts)
{
    /// <summary>Returns the Member with the first page of their Ledger, or null when there is no such Member.</summary>
    public async Task<MemberDetailView?> HandleAsync(
        Guid memberId, string? cursor, int? limit, CancellationToken cancellationToken)
    {
        if (await members.FindMemberAsync(memberId, cancellationToken) is not { } member)
        {
            return null;
        }

        var page = PageRequest.From(cursor, limit);
        var ledger = await accounts.FindAsync(memberId, page, cancellationToken);
        var entries = ledger?.LedgerEntries ?? [];
        return new MemberDetailView(member, page.ToPage(entries, e => new KeysetPosition(e.OccurredAt, e.Id), e => e));
    }
}

public sealed class ListMemberRequestsHandler(IAdminReader reader, TimeProvider clock)
{
    /// <summary>Returns the Member's Requests, or null when there is no such Member.</summary>
    public async Task<Page<MemberRequestView>?> HandleAsync(
        Guid memberId, string? cursor, int? limit, CancellationToken cancellationToken)
    {
        if (await reader.FindMemberAsync(memberId, cancellationToken) is null)
        {
            return null;
        }

        return await reader.ListMemberRequestsAsync(
            memberId, clock.GetUtcNow().UtcDateTime, PageRequest.From(cursor, limit), cancellationToken);
    }
}

public sealed class GetPoolOverviewHandler(IAdminReader reader, TimeProvider clock)
{
    public Task<PoolOverview> HandleAsync(CancellationToken cancellationToken) =>
        reader.GetPoolOverviewAsync(clock.GetUtcNow().UtcDateTime, cancellationToken);
}

/// <summary>Members with no activity for longer than the dormancy threshold (spec §5). Admin-only.</summary>
public sealed class ListDormantMembersHandler(IAdminReader reader, Identity.CoopSettings settings, TimeProvider clock)
{
    public async Task<IReadOnlyList<DormantMemberView>> HandleAsync(CancellationToken cancellationToken) =>
        await reader.ListDormantAsync(clock.GetUtcNow().UtcDateTime - settings.DormancyThreshold, cancellationToken);
}
