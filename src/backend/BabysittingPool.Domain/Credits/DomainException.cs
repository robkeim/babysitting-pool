namespace BabysittingPool.Domain.Credits;

/// <summary>Raised when an operation would violate a business invariant.</summary>
public sealed class DomainException(string message) : Exception(message);
