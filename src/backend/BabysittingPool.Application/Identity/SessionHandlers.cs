using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

public sealed record MemberView(Guid MemberId, string Email, string DisplayName, string Language, bool IsAdmin);

/// <summary>Who is behind a session cookie, keeping the session alive as it is used.</summary>
public sealed class ResolveSessionHandler(IIdentityStore store, CoopSettings settings, TimeProvider clock)
{
    /// <summary>Null if there is no such session, it has been idle too long, or its Member is closed.</summary>
    public async Task<MemberView?> HandleAsync(string? sessionSecret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionSecret))
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var session = await store.FindSessionAsync(Secrets.Hash(sessionSecret), cancellationToken);
        if (session is null || !session.IsActiveAt(now, settings.SessionIdleLifetime)
            || await store.FindMemberAsync(session.MemberId, cancellationToken) is not { IsClosed: false } member)
        {
            return null;
        }

        if (now - session.LastUsedAt > settings.SessionTouchInterval)
        {
            await store.TouchSessionAsync(session.Id, now, cancellationToken);
        }

        return new MemberView(member.Id, member.Email.Value, member.DisplayName.Value, member.Language.Tag, member.IsAdmin);
    }
}

public sealed class ChangeLanguageHandler(IIdentityStore store)
{
    public Task HandleAsync(Guid memberId, Language language, CancellationToken cancellationToken) =>
        store.SetLanguageAsync(memberId, language, cancellationToken);
}

public sealed class EndSessionHandler(IIdentityStore store)
{
    public Task HandleAsync(string sessionSecret, CancellationToken cancellationToken) =>
        store.EndSessionAsync(Secrets.Hash(sessionSecret), cancellationToken);
}
