using BabysittingPool.Application.Addresses;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Application.Identity;

/// <summary>A Member's profile as they see it: where they live, once for every Request at their house (spec §2A, ADR 0020).</summary>
public sealed record ProfileView(
    string? Area, string? Address, string? Commune, string? InseeCode, string? DepartmentNumber, string? Postcode, string? Instructions)
{
    public static ProfileView From(Member member) => member.Home is { } home
        ? new ProfileView(home.Area, home.Address, home.Commune, home.InseeCode, home.DepartmentNumber, home.Postcode, home.Instructions)
        : new ProfileView(null, null, null, null, null, null, null);
}

public sealed class GetMyProfileHandler(IIdentityStore store)
{
    /// <summary>Null if there is no such Member.</summary>
    public async Task<ProfileView?> HandleAsync(Guid memberId, CancellationToken cancellationToken) =>
        await store.FindMemberAsync(memberId, cancellationToken) is { } member ? ProfileView.From(member) : null;
}

/// <summary>The BAN id and label of the suggestion the Member picked (both empty to keep the saved address), and optional instructions (ADR 0020).</summary>
public sealed record UpdateMyProfileCommand(Guid MemberId, string? BanId, string? Label, string? Instructions);

public sealed class UpdateMyProfileHandler(IIdentityStore store, IAddressSuggester suggester)
{
    /// <summary>BAN accepts queries of 3 to 200 characters.</summary>
    private const int MaxLabelLength = 200;

    /// <summary>
    /// Null if there is no such Member. The pick is re-resolved through BAN, so only what BAN returns is stored: an unknown id
    /// or a missing label throws <see cref="DomainException"/> (<c>invalid_address</c>), instructions that are too long
    /// <c>invalid_instructions</c>.
    /// </summary>
    /// <exception cref="AddressServiceUnavailableException">BAN failed; nothing changed.</exception>
    public async Task<ProfileView?> HandleAsync(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await store.FindMemberAsync(command.MemberId, cancellationToken) is not { } member)
        {
            return null;
        }

        var label = command.Label?.Trim();
        var banId = command.BanId?.Trim();
        if (string.IsNullOrEmpty(banId) && string.IsNullOrEmpty(label) && member.Home is { } current)
        {
            // No new pick: only the instructions change, and the address stays the one BAN verified earlier.
            member.MoveTo(current.WithInstructions(command.Instructions));
            await store.SetHomeLocationAsync(member.Id, member.Home!, cancellationToken);
            return ProfileView.From(member);
        }

        if (string.IsNullOrEmpty(banId) || label is not { Length: >= SuggestAddressesHandler.MinimumQueryLength and <= MaxLabelLength })
        {
            throw new DomainException("Pick an address from the suggestions.", "invalid_address");
        }

        var pick = (await suggester.SuggestAsync(label, cancellationToken)).FirstOrDefault(s => s.BanId == banId)
            ?? throw new DomainException("Pick an address from the suggestions.", "invalid_address");

        member.MoveTo(HomeLocation.Create(
            pick.Label, pick.Commune, pick.InseeCode, pick.DepartmentNumber, pick.Postcode, command.Instructions));
        await store.SetHomeLocationAsync(member.Id, member.Home!, cancellationToken);
        return ProfileView.From(member);
    }
}
