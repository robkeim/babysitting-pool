namespace BabysittingPool.Application.Addresses;

/// <summary>
/// One address the geocoder offers (ADR 0020). <see cref="Type"/> is <c>housenumber</c> or <c>locality</c>; the
/// département number is the first part of the INSEE code's department ("80" for Amiens).
/// </summary>
public sealed record AddressSuggestion(
    string BanId,
    string Label,
    string Commune,
    string InseeCode,
    string DepartmentNumber,
    string Postcode,
    string Type);

/// <summary>Port for the address geocoder (BAN, ADR 0020).</summary>
public interface IAddressSuggester
{
    /// <exception cref="AddressServiceUnavailableException">The geocoder failed or timed out.</exception>
    Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(string query, CancellationToken cancellationToken);
}

public sealed class AddressServiceUnavailableException(Exception? inner = null)
    : Exception("The address service is unavailable.", inner);

public sealed class AddressQueryTooShortException() : Exception("The address query is too short.");

/// <summary>Suggestions for a partly typed address. The query is a partial home address, so nothing here logs it.</summary>
public sealed class SuggestAddressesHandler(IAddressSuggester suggester)
{
    public const int MinimumQueryLength = 3;

    /// <exception cref="AddressQueryTooShortException">Fewer than <see cref="MinimumQueryLength"/> characters after trimming.</exception>
    /// <exception cref="AddressServiceUnavailableException"/>
    public async Task<Page<AddressSuggestion>> HandleAsync(string? query, CancellationToken cancellationToken)
    {
        var trimmed = query?.Trim() ?? "";
        if (trimmed.Length < MinimumQueryLength)
        {
            throw new AddressQueryTooShortException();
        }

        return new Page<AddressSuggestion>(await suggester.SuggestAsync(trimmed, cancellationToken), null);
    }
}
