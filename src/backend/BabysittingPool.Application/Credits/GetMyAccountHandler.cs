namespace BabysittingPool.Application.Credits;

public sealed record GetMyAccountQuery(Guid MemberId, string? Cursor = null, int? Limit = null);

public sealed class GetMyAccountHandler(ICreditAccountReader reader)
{
    /// <summary>Returns the Member's Credit Account and Ledger, or null when they have none.</summary>
    public async Task<AccountView?> HandleAsync(GetMyAccountQuery query, CancellationToken cancellationToken = default)
    {
        var page = PageRequest.From(query.Cursor, query.Limit);
        var account = await reader.FindAsync(query.MemberId, page, cancellationToken);
        if (account is null)
        {
            return null;
        }

        return new AccountView(
            account.AvailableQuarterCredits,
            account.EscrowedQuarterCredits,
            page.ToPage(account.LedgerEntries, e => new KeysetPosition(e.OccurredAt, e.Id), e => e));
    }
}
