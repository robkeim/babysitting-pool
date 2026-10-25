using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

/// <summary>
/// Emails a sign-in link if the address belongs to an active Member and does nothing otherwise; the caller
/// answers the same either way so addresses cannot be probed.
/// </summary>
public sealed class RequestSignInLinkHandler(
    IIdentityStore store, IEmailSender email, AppLinks links, CoopSettings settings, TimeProvider clock)
{
    public async Task HandleAsync(EmailAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (await store.FindActiveMemberAsync(address, cancellationToken) is not { } member)
        {
            return;
        }

        var secret = Secrets.Generate();
        await store.AddSignInLinkAsync(
            SignInLink.Issue(member.Id, Secrets.Hash(secret), clock.GetUtcNow().UtcDateTime, settings.SignInLinkLifetime),
            cancellationToken);

        var link = links.SignIn(secret);
        await email.SendAsync(
            new EmailMessage(
                member.Email.Value,
                "Your sign-in link for the babysitting co-op",
                $"Use this link to sign in to the babysitting co-op (it works once and expires in {settings.SignInLinkLifetime.TotalHours:0} hours):\n\n{link}",
                link),
            cancellationToken);
    }
}

/// <summary>Turns a sign-in link into a Session.</summary>
public sealed class RedeemSignInLinkHandler(IIdentityStore store, TimeProvider clock)
{
    /// <summary>Null if the link is unknown, used or expired, or its Member is closed.</summary>
    public async Task<SignedIn?> HandleAsync(string token, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var link = await store.FindSignInLinkAsync(Secrets.Hash(token), cancellationToken);
        if (link is null || !link.IsUsableAt(now)
            || await store.FindMemberAsync(link.MemberId, cancellationToken) is not { IsClosed: false })
        {
            return null;
        }

        var sessionSecret = Secrets.Generate();
        var session = Session.Start(link.MemberId, Secrets.Hash(sessionSecret), now);
        return await store.TryRedeemSignInLinkAsync(link, session, now, cancellationToken) ? new SignedIn(sessionSecret) : null;
    }
}
