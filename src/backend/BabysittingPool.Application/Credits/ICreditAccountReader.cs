namespace BabysittingPool.Application.Credits;

/// <summary>Read port: queries project straight from the database (ADR 0002).</summary>
public interface ICreditAccountReader
{
    /// <summary>
    /// Returns the account and one over-fetched page of Ledger entries newest first (see <paramref name="page"/>), or null when the Member has no account.
    /// </summary>
    Task<AccountRead?> FindAsync(Guid memberId, PageRequest page, CancellationToken cancellationToken);
}

public sealed record AccountRead(
    long AvailableQuarterCredits,
    long EscrowedQuarterCredits,
    IReadOnlyList<LedgerEntryView> LedgerEntries);
