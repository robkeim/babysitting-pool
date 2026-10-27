using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Application.Offboarding;

public sealed record EraseMemberCommand(Guid MemberId);

/// <summary>
/// The Admin erases a closed Member's personal data (spec §2A, ADR 0014): name and email (also on the accepted Invitation)
/// become "Former member", Request addresses are cleared, and unused Invitations and sign-in links are deleted. Ledger amounts and ids stay.
/// </summary>
public sealed class EraseMemberHandler(IIdentityStore identity)
{
    /// <summary>
    /// False when there is no such Member. Erasing a Member who is not closed throws <see cref="DomainException"/>.
    /// Repeating it on an erased Member changes nothing.
    /// </summary>
    public async Task<bool> HandleAsync(EraseMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await identity.FindMemberAsync(command.MemberId, cancellationToken) is not { } member)
        {
            return false;
        }

        var formerEmail = member.Email;
        member.Erase();
        await identity.EraseMemberAsync(member, formerEmail, cancellationToken);
        return true;
    }
}
