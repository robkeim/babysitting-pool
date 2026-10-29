using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BabysittingPool.Application.Addresses;

namespace BabysittingPool.Infrastructure;

/// <summary>
/// Address suggestions from the Géoplateforme geocoder over the Base Adresse Nationale (ADR 0020). Only house
/// numbers and lieux-dits are kept. The query is a partial home address, so it is never logged or put in an
/// exception message (ADR 0010).
/// </summary>
public sealed class BanAddressSuggester(HttpClient http) : IAddressSuggester
{
    public const string BaseAddress = "https://data.geopf.fr/geocodage/";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private const int ResultLimit = 10;

    public async Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            var url = $"search?autocomplete=1&limit={ResultLimit}&q={Uri.EscapeDataString(query)}";
            using var response = await http.GetAsync(url, timeout.Token);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<BanResponse>(timeout.Token);
            return (body?.Features ?? [])
                .Select(feature => feature.Properties)
                .Where(p => p is { Type: "housenumber" or "locality" }
                    && p.Id is not null && p.Label is not null && p.City is not null
                    && p.CityCode is not null && p.DepartmentCode is not null && p.Postcode is not null)
                .Select(p => new AddressSuggestion(p!.Id!, p.Label!, p.City!, p.CityCode!, p.DepartmentCode!, p.Postcode!, p.Type!))
                .ToList();
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException
            && !cancellationToken.IsCancellationRequested)
        {
            // Dropped on purpose: HttpRequestException can carry the request URL, which holds the query.
            throw new AddressServiceUnavailableException();
        }
    }

    private sealed record BanResponse(List<BanFeature>? Features);

    private sealed record BanFeature(BanProperties? Properties);

    private sealed record BanProperties(
        string? Id,
        string? Label,
        string? Type,
        string? City,
        [property: JsonPropertyName("citycode")] string? CityCode,
        [property: JsonPropertyName("depcode")] string? DepartmentCode,
        string? Postcode);
}
