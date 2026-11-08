using BabysittingPool.Application;
using BabysittingPool.Application.Admin;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

internal sealed class AdminReader(BabysittingPoolDbContext db) : IAdminReader
{
    public async Task<MemberSummaryView?> FindMemberAsync(Guid memberId, CancellationToken cancellationToken) =>
        (await Summaries().SingleOrDefaultAsync(m => m.Id == memberId, cancellationToken))?.ToView();

    public async Task<Page<MemberRequestView>> ListMemberRequestsAsync(
        Guid memberId, DateTime now, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = await db.Requests.AsNoTracking()
            .Where(r => r.RequesterId == memberId || r.SitterId == memberId)
            .ToKeysetPageAsync(page, r => r.StartsAt, r => r.Id, descending: true, cancellationToken);

        return page.ToPage(rows, r => new KeysetPosition(r.StartsAt, r.Id), r => new MemberRequestView(
            r.Id,
            r.RequesterId == memberId ? MemberRequestRole.Requester : MemberRequestRole.Sitter,
            r.StartsAt,
            r.DurationMinutes,
            r.ChildCount,
            r.Area,
            r.Cost.QuarterCredits,
            Request.StatusAt(r.Status, r.ClaimCutoffAt, r.StartsAt, now)));
    }

    public async Task<PoolOverview> GetPoolOverviewAsync(DateTime now, CancellationToken cancellationToken)
    {
        // Amounts are summed here: Credits is a value-converted type that SQL cannot add up.
        var balances = await db.CreditAccounts.AsNoTracking()
            .Select(a => new { a.Available, a.Escrowed }).ToListAsync(cancellationToken);
        var burned = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.Kind == LedgerEntryKind.Demurrage || e.Kind == LedgerEntryKind.Burn)
            .Select(e => new { e.Kind, e.Amount }).ToListAsync(cancellationToken);
        var openMembers = await db.Members.AsNoTracking().CountAsync(m => m.ClosedAt == null, cancellationToken);

        // Grouped by the facts StatusAt reads, so an overdue Posted Request counts as Expired before Settle runs.
        var groups = await db.Requests.AsNoTracking()
            .GroupBy(r => new { r.Status, CutoffPassed = r.ClaimCutoffAt <= now, Started = r.StartsAt <= now })
            .Select(g => new { g.Key.Status, g.Key.CutoffPassed, g.Key.Started, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var counts = groups
            .GroupBy(g => Request.StatusAt(
                g.Status,
                g.CutoffPassed ? DateTime.MinValue : DateTime.MaxValue,
                g.Started ? DateTime.MinValue : DateTime.MaxValue,
                now))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        return new PoolOverview(
            balances.Sum(b => b.Available.QuarterCredits + b.Escrowed.QuarterCredits),
            balances.Sum(b => b.Escrowed.QuarterCredits),
            burned.Where(e => e.Kind == LedgerEntryKind.Demurrage).Sum(e => e.Amount.QuarterCredits),
            burned.Where(e => e.Kind == LedgerEntryKind.Burn).Sum(e => e.Amount.QuarterCredits),
            openMembers,
            new RequestCounts(
                counts.GetValueOrDefault(RequestStatus.Posted),
                counts.GetValueOrDefault(RequestStatus.Claimed),
                counts.GetValueOrDefault(RequestStatus.Completed),
                counts.GetValueOrDefault(RequestStatus.Canceled),
                counts.GetValueOrDefault(RequestStatus.Expired)));
    }

    public async Task<IReadOnlyList<DormantMemberView>> ListDormantAsync(
        DateTime activeSince, CancellationToken cancellationToken)
    {
        // A closed Member is out of the pool, so their last activity must not keep aging onto the list (#46).
        var members = await db.Members.AsNoTracking()
            .Where(m => m.ClosedAt == null && !m.IsAdmin)
            .Join(db.CreditAccounts.AsNoTracking(), m => m.Id, a => a.MemberId, (m, a) => new
            {
                m.Id,
                m.DisplayName,
                m.Email,
                m.CreatedAt,
                a.Available,
            })
            .ToListAsync(cancellationToken);

        var asRequester = await db.Requests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Completed)
            .GroupBy(r => r.RequesterId)
            .Select(g => new { MemberId = g.Key, At = g.Max(r => r.StartsAt) })
            .ToListAsync(cancellationToken);
        var asSitter = await db.Requests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Completed && r.SitterId != null)
            .GroupBy(r => r.SitterId!.Value)
            .Select(g => new { MemberId = g.Key, At = g.Max(r => r.StartsAt) })
            .ToListAsync(cancellationToken);
        var lastCompleted = asRequester.Concat(asSitter)
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.At));

        return members
            .Select(m => new DormantMemberView(
                m.Id,
                m.DisplayName,
                m.Email,
                lastCompleted.TryGetValue(m.Id, out var at) ? at : m.CreatedAt,
                m.Available.QuarterCredits))
            .Where(m => m.LastActivityAt < activeSince)
            .OrderBy(m => m.LastActivityAt).ThenBy(m => m.Id)
            .ToList();
    }

    // Credits is value-converted, so the query carries it as is and the view reads its quarter-credits afterwards.
    // Member-initialized (not a constructor) so ordering and paging can see through the projection.
    private IQueryable<SummaryRow> Summaries() =>
        db.Members.AsNoTracking()
            .Join(db.CreditAccounts.AsNoTracking(), m => m.Id, a => a.MemberId, (m, a) => new SummaryRow
            {
                Id = m.Id,
                DisplayName = m.DisplayName,
                Email = m.Email,
                IsAdmin = m.IsAdmin,
                CreatedAt = m.CreatedAt,
                ClosedAt = m.ClosedAt,
                Available = a.Available,
                Escrowed = a.Escrowed,
            });

    private sealed class SummaryRow
    {
        public Guid Id { get; init; }

        public string DisplayName { get; init; } = "";

        public string Email { get; init; } = "";

        public bool IsAdmin { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? ClosedAt { get; init; }

        public Credits Available { get; init; }

        public Credits Escrowed { get; init; }

        public MemberSummaryView ToView() => new(
            Id, DisplayName, Email, IsAdmin, CreatedAt, ClosedAt, Available.QuarterCredits, Escrowed.QuarterCredits);
    }
}
