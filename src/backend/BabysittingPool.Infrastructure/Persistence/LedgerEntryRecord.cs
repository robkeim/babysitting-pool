using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Infrastructure.Persistence;

/// <summary>One row of the append-only Ledger; the database rejects UPDATE and DELETE.</summary>
public sealed class LedgerEntryRecord
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }

    /// <summary>The Request an Escrow, Refund or Payout belongs to; null for movements that have none.</summary>
    public Guid? RequestId { get; set; }

    public LedgerEntryKind Kind { get; set; }
    public Credits Amount { get; set; }
    public DateTime OccurredAt { get; set; }
}
