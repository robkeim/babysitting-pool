using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

/// <summary>Persistence port for Members, Invitations, sign-in links and Sessions.</summary>
public interface IIdentityStore
{
    Task<bool> AdminExistsAsync(CancellationToken cancellationToken);

    Task<bool> HasLiveAdminInvitationAsync(DateTime now, CancellationToken cancellationToken);

    /// <summary>The Member with this address who is not closed.</summary>
    Task<Member?> FindActiveMemberAsync(EmailAddress email, CancellationToken cancellationToken);

    Task<Member?> FindMemberAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>Closes the Member and ends all their Sessions. Does nothing if they are already closed.</summary>
    Task CloseMemberAsync(Guid memberId, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// In one transaction: saves the erased Member's name and email, clears their profile address and the address of
    /// every Request they posted, and deletes the Invitations sent to <paramref name="formerEmail"/> that are still unused and the
    /// sign-in links issued to the Member. The Ledger is untouched (ADR 0014).
    /// </summary>
    Task EraseMemberAsync(Member member, EmailAddress formerEmail, CancellationToken cancellationToken);

    /// <summary>Saves the area and address on the Member's profile. Does nothing if they are closed.</summary>
    Task SetHomeLocationAsync(Guid memberId, HomeLocation home, CancellationToken cancellationToken);

    Task SetLanguageAsync(Guid memberId, Language language, CancellationToken cancellationToken);

    Task AddInvitationAsync(Invitation invitation, CancellationToken cancellationToken);

    Task<Invitation?> FindInvitationAsync(string secretHash, CancellationToken cancellationToken);

    Task<Invitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken);

    /// <summary>An Invitation to this address that was never accepted, whether still valid or expired.</summary>
    Task<Invitation?> FindUnacceptedInvitationAsync(EmailAddress email, CancellationToken cancellationToken);

    /// <summary>Every Invitation, newest first.</summary>
    Task<IReadOnlyList<Invitation>> ListInvitationsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the secret and validity of an Invitation that has not been accepted. Returns false, changing
    /// nothing, if it was accepted meanwhile.
    /// </summary>
    Task<bool> TryReissueInvitationAsync(Invitation reissued, CancellationToken cancellationToken);

    /// <summary>
    /// In one transaction: consumes the Invitation, adds the Member with a Credit Account holding the Starting
    /// Balance, and adds the Session. Returns false, changing nothing, if the Invitation was consumed meanwhile
    /// or the address is already a Member.
    /// </summary>
    Task<bool> TryAcceptInvitationAsync(
        Invitation invitation, Member member, Session session, DateTime now, CancellationToken cancellationToken);

    Task AddSignInLinkAsync(SignInLink link, CancellationToken cancellationToken);

    Task<SignInLink?> FindSignInLinkAsync(string secretHash, CancellationToken cancellationToken);

    /// <summary>Consumes the link and adds the Session in one transaction; false if it was consumed meanwhile.</summary>
    Task<bool> TryRedeemSignInLinkAsync(SignInLink link, Session session, DateTime now, CancellationToken cancellationToken);

    Task AddSessionAsync(Session session, CancellationToken cancellationToken);

    Task<Session?> FindSessionAsync(string secretHash, CancellationToken cancellationToken);

    Task TouchSessionAsync(Guid sessionId, DateTime now, CancellationToken cancellationToken);

    Task EndSessionAsync(string secretHash, CancellationToken cancellationToken);
}
