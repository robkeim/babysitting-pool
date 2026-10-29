using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Infrastructure.Persistence;

public sealed class MemberRecord
{
    public Guid Id { get; set; }

    public string Email { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string Language { get; set; } = "en-US";

    public string ColorScheme { get; set; } = "system";

    public bool IsAdmin { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>The BAN label.</summary>
    public string? Address { get; set; }

    public string? Commune { get; set; }

    public string? InseeCode { get; set; }

    public string? DepartmentNumber { get; set; }

    public string? Postcode { get; set; }

    public string? Instructions { get; set; }

    /// <summary>Null until the Member has picked an address.</summary>
    public HomeLocation? ToHome() =>
        Address is { } address && Commune is { } commune && InseeCode is { } inseeCode
            && DepartmentNumber is { } departmentNumber && Postcode is { } postcode
            ? HomeLocation.Restore(address, commune, inseeCode, departmentNumber, postcode, Instructions)
            : null;
}

public sealed class InvitationRecord
{
    public Guid Id { get; set; }

    public string Email { get; set; } = "";

    public bool IsAdmin { get; set; }

    public string SecretHash { get; set; } = "";

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    /// <summary>Optimistic concurrency token: two people submitting one Invitation cannot both win (ADR 0004).</summary>
    public int Version { get; set; }
}

public sealed class SignInLinkRecord
{
    public Guid Id { get; set; }

    public Guid MemberId { get; set; }

    public string SecretHash { get; set; } = "";

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    /// <summary>Optimistic concurrency token, as on <see cref="InvitationRecord"/>.</summary>
    public int Version { get; set; }
}

public sealed class SessionRecord
{
    public Guid Id { get; set; }

    public Guid MemberId { get; set; }

    public string SecretHash { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime LastUsedAt { get; set; }
}
