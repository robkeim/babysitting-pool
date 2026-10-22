namespace BabysittingPool.Domain.Identity;

/// <summary>How a Member is shown to others: 1 to 60 characters after trimming.</summary>
public sealed record DisplayName
{
    public const int MaxLength = 60;

    private DisplayName(string value) => Value = value;

    public string Value { get; }

    public static bool TryCreate(string? input, out DisplayName name)
    {
        name = null!;
        var trimmed = input?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength || trimmed.Any(char.IsControl))
        {
            return false;
        }

        name = new DisplayName(trimmed);
        return true;
    }

    public static DisplayName Restore(string value) => new(value);

    public override string ToString() => Value;
}
