namespace BabysittingPool.Domain.Credits;

/// <summary>
/// A member's credit balances (spec §2 and §4). Available balance never drops below zero.
/// </summary>
public sealed class CreditAccount
{
    public const decimal StartingBalance = 10.0m;

    private CreditAccount(decimal available, decimal escrowed)
    {
        Available = available;
        Escrowed = escrowed;
    }

    public decimal Available { get; private set; }
    public decimal Escrowed { get; private set; }

    /// <summary>Opens an account for a newly activated member with the starting balance.</summary>
    public static CreditAccount Open() => new(StartingBalance, 0m);

    /// <summary>Rehydrates an account from persisted balances.</summary>
    public static CreditAccount Restore(decimal available, decimal escrowed)
    {
        if (available < 0m)
        {
            throw new DomainException("Available balance cannot be negative.");
        }

        if (escrowed < 0m)
        {
            throw new DomainException("Escrowed balance cannot be negative.");
        }

        return new CreditAccount(available, escrowed);
    }

    /// <summary>Moves the cost of a posted request from Available into Escrow.</summary>
    public void Escrow(decimal amount)
    {
        RequirePositive(amount);
        if (Available < amount)
        {
            throw new DomainException(
                $"Insufficient balance: {Available} available, {amount} required.",
                "insufficient_credits");
        }

        Available -= amount;
        Escrowed += amount;
    }

    /// <summary>Returns escrowed credits to Available (cancelled or expired request).</summary>
    public void Refund(decimal amount)
    {
        RequirePositive(amount);
        RequireEscrowed(amount);

        Escrowed -= amount;
        Available += amount;
    }

    /// <summary>Releases escrowed credits to the sitter's Available balance.</summary>
    public void PayOut(decimal amount, CreditAccount sitter)
    {
        ArgumentNullException.ThrowIfNull(sitter);
        if (ReferenceEquals(this, sitter))
        {
            throw new DomainException("A member cannot pay out to themselves.");
        }

        RequirePositive(amount);
        RequireEscrowed(amount);

        Escrowed -= amount;
        sitter.Available += amount;
    }

    /// <summary>
    /// Burns up to <paramref name="amount"/> from Available (Demurrage, spec §5) and returns what was burned. Never
    /// drives Available below zero and never touches Escrow.
    /// </summary>
    public decimal Burn(decimal amount)
    {
        RequirePositive(amount);

        var burned = Math.Min(amount, Available);
        Available -= burned;
        return burned;
    }

    /// <summary>
    /// Takes the whole Available balance out of circulation when a member is offboarded (spec §2A) and returns the
    /// amount burned. Escrow must already have been released.
    /// </summary>
    public decimal BurnAll()
    {
        if (Escrowed != 0m)
        {
            throw new DomainException($"{Escrowed} is still escrowed; release it before burning.", "escrow_outstanding");
        }

        var burned = Available;
        Available = 0m;
        return burned;
    }

    private void RequireEscrowed(decimal amount)
    {
        if (Escrowed < amount)
        {
            throw new DomainException($"Only {Escrowed} is escrowed; cannot release {amount}.");
        }
    }

    private static void RequirePositive(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new DomainException("Amount must be greater than zero.");
        }
    }
}
