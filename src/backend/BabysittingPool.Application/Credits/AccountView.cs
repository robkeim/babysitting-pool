using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Application.Credits;

public sealed record AccountView(
    long AvailableQuarterCredits,
    long EscrowedQuarterCredits,
    Page<LedgerEntryView> Ledger);

public sealed record LedgerEntryView(Guid Id, LedgerEntryKind Kind, long AmountQuarterCredits, DateTime OccurredAt);
