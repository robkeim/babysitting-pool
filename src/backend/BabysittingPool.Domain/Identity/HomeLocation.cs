using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Domain.Identity;

/// <summary>
/// Where a Member lives (spec §2A, ADR 0020): a verified BAN <see cref="Address"/> label only a counterparty may see
/// (spec §4.6), the commune it is in, and optional <see cref="Instructions"/> for what a geocoder cannot say. The
/// <see cref="Area"/> other Members may see is derived from the commune, never typed.
/// </summary>
public sealed record HomeLocation
{
    public const int MaxInstructionsLength = 300;

    private HomeLocation(
        string address, string commune, string inseeCode, string departmentNumber, string postcode, string? instructions)
    {
        Address = address;
        Commune = commune;
        InseeCode = inseeCode;
        DepartmentNumber = departmentNumber;
        Postcode = postcode;
        Instructions = instructions;
    }

    /// <summary>The BAN label ("12 Rue de la Paix 80000 Amiens").</summary>
    public string Address { get; }

    public string Commune { get; }

    /// <summary>Grouping and equality of Areas use this, the text is only a label (ADR 0020).</summary>
    public string InseeCode { get; }

    public string DepartmentNumber { get; }

    public string Postcode { get; }

    public string? Instructions { get; }

    /// <summary>The commune and its département number, for example "Amiens (80)".</summary>
    public string Area => $"{Commune} ({DepartmentNumber})";

    /// <summary>
    /// Throws <see cref="DomainException"/> (<c>invalid_address</c>) if a part of the address is blank, or
    /// <c>invalid_instructions</c> if the instructions are over <see cref="MaxInstructionsLength"/> characters. Blank instructions mean none.
    /// </summary>
    public static HomeLocation Create(
        string? address, string? commune, string? inseeCode, string? departmentNumber, string? postcode, string? instructions)
    {
        var trimmedInstructions = instructions?.Trim();
        if (trimmedInstructions?.Length > MaxInstructionsLength)
        {
            throw new DomainException(
                $"The instructions are at most {MaxInstructionsLength} characters.", "invalid_instructions");
        }

        return new HomeLocation(
            RequireText(address), RequireText(commune), RequireText(inseeCode), RequireText(departmentNumber), RequireText(postcode),
            string.IsNullOrEmpty(trimmedInstructions) ? null : trimmedInstructions);
    }

    /// <summary>The same address with other instructions (a Member edits them without picking again). Throws as <see cref="Create"/> does.</summary>
    public HomeLocation WithInstructions(string? instructions) =>
        Create(Address, Commune, InseeCode, DepartmentNumber, Postcode, instructions);

    public static HomeLocation Restore(
        string address, string commune, string inseeCode, string departmentNumber, string postcode, string? instructions) =>
        new(address, commune, inseeCode, departmentNumber, postcode, instructions);

    private static string RequireText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? throw new DomainException("The address is required.", "invalid_address")
            : trimmed;
    }
}
