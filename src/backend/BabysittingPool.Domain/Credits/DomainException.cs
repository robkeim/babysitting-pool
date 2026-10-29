namespace BabysittingPool.Domain.Credits;

/// <summary>
/// Raised when an operation would violate a business invariant. <see cref="Code"/> is the stable
/// machine-readable code the API reports (ADR 0006).
/// </summary>
public sealed class DomainException(string message, string code = DomainException.DefaultCode) : Exception(message)
{
    public const string DefaultCode = "domain_rule_violated";

    /// <summary>The thing acted on is no longer in the right state; the API reports it as a conflict (409).</summary>
    public const string InvalidStateCode = "invalid_state";

    public string Code { get; } = code;
}
