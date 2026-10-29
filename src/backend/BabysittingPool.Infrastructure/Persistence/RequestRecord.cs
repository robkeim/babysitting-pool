using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Requests;

namespace BabysittingPool.Infrastructure.Persistence;

/// <summary>Persistence model of a Request; the domain <see cref="Request"/> is mapped to and from it.</summary>
public sealed class RequestRecord
{
    public Guid Id { get; set; }

    public Guid RequesterId { get; set; }

    public Guid? SitterId { get; set; }

    public DateTime StartsAt { get; set; }

    public int DurationMinutes { get; set; }

    public int ChildCount { get; set; }

    public BabysittingLocation Location { get; set; }

    public TransportOption Transport { get; set; }

    public MealOption Meals { get; set; }

    public string? Area { get; set; }

    public string? Address { get; set; }

    public string? Instructions { get; set; }

    public Credits Cost { get; set; }

    public DateTime ClaimCutoffAt { get; set; }

    public DateTime PostedAt { get; set; }

    public RequestStatus Status { get; set; }

    /// <summary>Optimistic concurrency token (ADR 0004).</summary>
    public int Version { get; set; }
}
