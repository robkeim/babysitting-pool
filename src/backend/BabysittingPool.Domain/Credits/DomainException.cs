namespace BabysittingPool.Domain.Credits;

/// <summary>
/// Raised when an operation would violate a business invariant. <see cref="Code"/> is the stable
/// machine-readable code the API reports (ADR 0006).
/// </summary>
public sealed class DomainException(string message, string code = DomainException.DefaultCode) : Exception(message)
{
    public const string DefaultCode = "domain_rule_violated";

    public string Code { get; } = code;
}
