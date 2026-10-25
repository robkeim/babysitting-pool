using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Infrastructure.Persistence;

/// <summary>Persistence model of a Credit Account; the domain <see cref="CreditAccount"/> is mapped to and from it.</summary>
public sealed class CreditAccountRecord
{
    public Guid MemberId { get; set; }

    public Credits Available { get; set; }

    public Credits Escrowed { get; set; }

    /// <summary>Optimistic concurrency token (ADR 0004).</summary>
    public int Version { get; set; }
}
