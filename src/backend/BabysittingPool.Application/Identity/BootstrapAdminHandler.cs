using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

/// <summary>
/// Gets the first Admin in (ADR 0011): when no Admin exists and no Admin Invitation is outstanding, invites
/// the configured address.
/// </summary>
public sealed class BootstrapAdminHandler(IIdentityStore store, InvitationIssuer issue, TimeProvider clock)
{
    public async Task HandleAsync(EmailAddress? adminEmail, CancellationToken cancellationToken)
    {
        if (await store.AdminExistsAsync(cancellationToken))
        {
            return;
        }

        if (adminEmail is null)
        {
            throw new InvalidOperationException(
                "The co-op has no Admin and Bootstrap:AdminEmail is not set, so nobody could ever sign in.");
        }

        if (!await store.HasLiveAdminInvitationAsync(clock.GetUtcNow().UtcDateTime, cancellationToken))
        {
            await issue.HandleAsync(new IssueInvitationCommand(adminEmail, IsAdmin: true), cancellationToken);
        }
    }
}
