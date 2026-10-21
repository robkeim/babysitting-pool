namespace BabysittingPool.Application.Credits;

public sealed record AccountView(
    long AvailableQuarterCredits,
    long EscrowedQuarterCredits,
    Page<LedgerEntryView> Ledger);

public sealed record LedgerEntryView(Guid Id, string Kind, long AmountQuarterCredits, DateTime OccurredAt);

/// <summary>A list response: <c>{ items, nextCursor }</c> (ADR 0006).</summary>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
