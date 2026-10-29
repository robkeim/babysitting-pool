using BabysittingPool.Application.Addresses;

namespace BabysittingPool.Api;

/// <summary>
/// Stands in for BAN in the E2E host (<see cref="TestEndpoints.Environment"/>) so browser journeys never call the
/// real service. A suggestion matches when every word of the query appears in its label.
/// </summary>
internal sealed class E2eAddressSuggester : IAddressSuggester
{
    private static readonly AddressSuggestion[] Known =
    [
        Housenumber("00001_1_12", "12 River Rd 80000 Riverside", "Riverside", "00001", "80000"),
        Housenumber("00002_1_3", "3 Hill St 80100 Hilltop Meadows", "Hilltop Meadows", "00002", "80100"),
        Housenumber("00003_1_8", "8 Mill Ln 80200 Old Mill District", "Old Mill District", "00003", "80200"),
        Housenumber("00004_1_5", "5 Maple Ct 80300 Maple Court", "Maple Court", "00004", "80300"),
        Housenumber("00005_1_9", "9 Cedar Ln 80400 Cedar Lane", "Cedar Lane", "00005", "80400"),
        Housenumber("80021_6115_12", "12 Rue de la Paix 80000 Amiens", "Amiens", "80021", "80000"),
    ];

    public Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(string query, CancellationToken cancellationToken)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Task.FromResult<IReadOnlyList<AddressSuggestion>>(Known
            .Where(k => words.All(w => k.Label.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToList());
    }

    private static AddressSuggestion Housenumber(string banId, string label, string commune, string inseeCode, string postcode) =>
        new(banId, label, commune, inseeCode, "80", postcode, "housenumber");
}
