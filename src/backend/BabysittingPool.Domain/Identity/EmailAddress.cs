using System.Net.Mail;

namespace BabysittingPool.Domain.Identity;

/// <summary>An email address, trimmed and lower-cased so that equal addresses compare equal.</summary>
public sealed record EmailAddress
{
    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static bool TryParse(string? input, out EmailAddress email)
    {
        email = null!;
        var trimmed = input?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 254 || !MailAddress.TryCreate(trimmed, out var parsed)
            || !string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase)
            || !parsed.Host.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

#pragma warning disable CA1308 // Normalised for comparison and storage, never for security decisions.
        email = new EmailAddress(trimmed.ToLowerInvariant());
#pragma warning restore CA1308
        return true;
    }

    /// <summary>Rehydrates an address that was validated when it was stored.</summary>
    public static EmailAddress Restore(string value) => new(value);

    public override string ToString() => Value;
}
