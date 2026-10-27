using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Domain.Identity;

/// <summary>
/// Where a Member lives (spec §2A): a rough <see cref="Area"/> other Members may see and the exact
/// <see cref="Address"/> only a counterparty may see (spec §4.6). Both are required and trimmed.
/// </summary>
public sealed record HomeLocation
{
    public const int MaxAreaLength = 100;
    public const int MaxAddressLength = 200;

    private HomeLocation(string area, string address)
    {
        Area = area;
        Address = address;
    }

    public string Area { get; }

    public string Address { get; }

    /// <summary>Throws <see cref="DomainException"/> (<c>invalid_area</c>, <c>invalid_address</c>) if either part is blank or too long.</summary>
    public static HomeLocation Create(string? area, string? address) =>
        new(RequireText(area, MaxAreaLength, "area"), RequireText(address, MaxAddressLength, "address"));

    public static HomeLocation Restore(string area, string address) => new(area, address);

    private static string RequireText(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
        {
            throw new DomainException($"The {field} is required and at most {maxLength} characters.", $"invalid_{field}");
        }

        return trimmed;
    }
}
