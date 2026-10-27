using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

/// <summary>A Member's profile as they see it: where they live, once for every Request at their house (spec §2A).</summary>
public sealed record ProfileView(string? Area, string? Address)
{
    public static ProfileView From(Member member) => new(member.Home?.Area, member.Home?.Address);
}

public sealed class GetMyProfileHandler(IIdentityStore store)
{
    /// <summary>Null if there is no such Member.</summary>
    public async Task<ProfileView?> HandleAsync(Guid memberId, CancellationToken cancellationToken) =>
        await store.FindMemberAsync(memberId, cancellationToken) is { } member ? ProfileView.From(member) : null;
}

public sealed record UpdateMyProfileCommand(Guid MemberId, string? Area, string? Address);

public sealed class UpdateMyProfileHandler(IIdentityStore store)
{
    /// <summary>Null if there is no such Member. A blank or too long area or address throws <see cref="DomainException"/>.</summary>
    public async Task<ProfileView?> HandleAsync(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FindMemberAsync(command.MemberId, cancellationToken) is not { } member)
        {
            return null;
        }

        member.MoveTo(HomeLocation.Create(command.Area, command.Address));
        await store.SetHomeLocationAsync(member.Id, member.Home!, cancellationToken);
        return ProfileView.From(member);
    }
}
