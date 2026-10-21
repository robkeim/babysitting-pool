namespace BabysittingPool.Application.Credits;

/// <summary>Read port: queries project straight from the database (ADR 0002).</summary>
public interface ICreditAccountReader
{
    /// <summary>
    /// Returns the account and up to <paramref name="take"/> Ledger entries newest first, starting after
    /// <paramref name="after"/>, or null when the Member has no account.
    /// </summary>
    Task<AccountRead?> FindAsync(Guid memberId, LedgerCursor? after, int take, CancellationToken cancellationToken);
}

public sealed record AccountRead(
    long AvailableQuarterCredits,
    long EscrowedQuarterCredits,
    IReadOnlyList<LedgerEntryView> LedgerEntries);

/// <summary>Position in the Ledger ordering (newest first, ties broken by id).</summary>
public sealed record LedgerCursor(DateTime OccurredAt, Guid Id);
