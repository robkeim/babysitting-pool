using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

/// <summary>An Admin's row for a Member: who they are and whether the account is open, closed or erased (spec §2A).</summary>
public sealed record AdminMemberView(
    Guid Id, string DisplayName, string Email, bool IsAdmin, DateTime CreatedAt, DateTime? ClosedAt, bool IsErased)
{
    internal static AdminMemberView From(Member member) => new(
        member.Id,
        member.DisplayName.Value,
        member.Email.Value,
        member.IsAdmin,
        member.CreatedAt,
        member.ClosedAt,
        member.IsClosed && member.Email.Value == Member.Tombstone);
}

/// <summary>Every Member, open and closed, so the Admin has someone to offboard or erase (spec §2A).</summary>
public sealed class ListMembersHandler(IIdentityStore store)
{
    public async Task<IReadOnlyList<AdminMemberView>> HandleAsync(CancellationToken cancellationToken) =>
        (await store.ListMembersAsync(cancellationToken)).Select(AdminMemberView.From).ToList();
}
