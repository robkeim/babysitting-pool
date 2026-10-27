using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

internal sealed class IdentityStore(BabysittingPoolDbContext db) : IIdentityStore
{
    public async Task<bool> AdminExistsAsync(CancellationToken cancellationToken) =>
        await db.Members.AnyAsync(m => m.IsAdmin && m.ClosedAt == null, cancellationToken);

    public async Task<bool> HasLiveAdminInvitationAsync(DateTime now, CancellationToken cancellationToken) =>
        await db.Invitations.AnyAsync(i => i.IsAdmin && i.ConsumedAt == null && i.ExpiresAt > now, cancellationToken);

    public async Task<Member?> FindActiveMemberAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        var record = await db.Members.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Email == email.Value && m.ClosedAt == null, cancellationToken);
        return record is null ? null : ToDomain(record);
    }

    public async Task<Member?> FindMemberAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var record = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<Member>> ListMembersAsync(CancellationToken cancellationToken)
    {
        var records = await db.Members.AsNoTracking()
            .OrderBy(m => m.ClosedAt != null).ThenBy(m => m.DisplayName).ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);
        return records.Select(ToDomain).ToList();
    }

    public async Task CloseMemberAsync(Guid memberId, DateTime now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Members.Where(m => m.Id == memberId && m.ClosedAt == null)
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.ClosedAt, now), cancellationToken);
        await db.Sessions.Where(s => s.MemberId == memberId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task EraseMemberAsync(Member member, EmailAddress formerEmail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(formerEmail);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Members.Where(m => m.Id == member.Id && m.ClosedAt != null)
            .ExecuteUpdateAsync(
                m => m.SetProperty(x => x.Email, member.Email.Value)
                    .SetProperty(x => x.DisplayName, member.DisplayName.Value)
                    .SetProperty(x => x.Area, (string?)null)
                    .SetProperty(x => x.Address, (string?)null),
                cancellationToken);
        await db.Requests.Where(r => r.RequesterId == member.Id)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.Address, (string?)null), cancellationToken);
        await db.Invitations.Where(i => i.Email == formerEmail.Value && i.ConsumedAt == null)
            .ExecuteDeleteAsync(cancellationToken);
        await db.Invitations.Where(i => i.Email == formerEmail.Value && i.ConsumedAt != null)
            .ExecuteUpdateAsync(i => i.SetProperty(x => x.Email, member.Email.Value), cancellationToken);
        await db.SignInLinks.Where(l => l.MemberId == member.Id).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetHomeLocationAsync(Guid memberId, HomeLocation home, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(home);
        await db.Members.Where(m => m.Id == memberId && m.ClosedAt == null)
            .ExecuteUpdateAsync(
                m => m.SetProperty(x => x.Area, home.Area).SetProperty(x => x.Address, home.Address), cancellationToken);
    }

    public async Task SetLanguageAsync(Guid memberId, Language language, CancellationToken cancellationToken) =>
        await db.Members.Where(m => m.Id == memberId)
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.Language, language.Tag), cancellationToken);

    public async Task AddInvitationAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        db.Invitations.Add(new InvitationRecord
        {
            Id = invitation.Id,
            Email = invitation.Email.Value,
            IsAdmin = invitation.IsAdmin,
            SecretHash = invitation.SecretHash,
            IssuedAt = invitation.IssuedAt,
            ExpiresAt = invitation.ExpiresAt,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Invitation?> FindInvitationAsync(string secretHash, CancellationToken cancellationToken) =>
        ToDomain(await db.Invitations.AsNoTracking().SingleOrDefaultAsync(i => i.SecretHash == secretHash, cancellationToken));

    public async Task<Invitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken) =>
        ToDomain(await db.Invitations.AsNoTracking().SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken));

    public async Task<Invitation?> FindUnacceptedInvitationAsync(EmailAddress email, CancellationToken cancellationToken) =>
        ToDomain(await db.Invitations.AsNoTracking()
            .Where(i => i.Email == email.Value && i.ConsumedAt == null)
            .OrderByDescending(i => i.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken));

    public async Task<IReadOnlyList<Invitation>> ListInvitationsAsync(CancellationToken cancellationToken)
    {
        var records = await db.Invitations.AsNoTracking()
            .OrderByDescending(i => i.IssuedAt).ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);
        return records.Select(r => ToDomain(r)!).ToList();
    }

    public async Task<bool> TryReissueInvitationAsync(Invitation reissued, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reissued);
        var record = await db.Invitations.SingleAsync(i => i.Id == reissued.Id, cancellationToken);
        if (record.ConsumedAt is not null)
        {
            return false;
        }

        record.SecretHash = reissued.SecretHash;
        record.IssuedAt = reissued.IssuedAt;
        record.ExpiresAt = reissued.ExpiresAt;
        record.Version++;
        return await TrySaveAsync(cancellationToken);
    }

    public async Task<bool> TryAcceptInvitationAsync(
        Invitation invitation, Member member, Session session, DateTime now, CancellationToken cancellationToken)
    {
        var record = await db.Invitations.SingleAsync(i => i.Id == invitation.Id, cancellationToken);
        record.ConsumedAt = now;
        record.Version++;

        db.Members.Add(new MemberRecord
        {
            Id = member.Id,
            Email = member.Email.Value,
            DisplayName = member.DisplayName.Value,
            Language = member.Language.Tag,
            IsAdmin = member.IsAdmin,
            CreatedAt = member.CreatedAt,
        });
        AddCreditAccount(member.Id, CreditAccount.Open(), now);
        AddSession(session);
        return await TrySaveAsync(cancellationToken);
    }

    public async Task AddSignInLinkAsync(SignInLink link, CancellationToken cancellationToken)
    {
        db.SignInLinks.Add(new SignInLinkRecord
        {
            Id = link.Id,
            MemberId = link.MemberId,
            SecretHash = link.SecretHash,
            ExpiresAt = link.ExpiresAt,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SignInLink?> FindSignInLinkAsync(string secretHash, CancellationToken cancellationToken)
    {
        var r = await db.SignInLinks.AsNoTracking().SingleOrDefaultAsync(l => l.SecretHash == secretHash, cancellationToken);
        return r is null ? null : SignInLink.Restore(r.Id, r.MemberId, r.SecretHash, r.ExpiresAt, r.ConsumedAt);
    }

    public async Task<bool> TryRedeemSignInLinkAsync(
        SignInLink link, Session session, DateTime now, CancellationToken cancellationToken)
    {
        var record = await db.SignInLinks.SingleAsync(l => l.Id == link.Id, cancellationToken);
        record.ConsumedAt = now;
        record.Version++;
        AddSession(session);
        return await TrySaveAsync(cancellationToken);
    }

    public async Task AddSessionAsync(Session session, CancellationToken cancellationToken)
    {
        AddSession(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Session?> FindSessionAsync(string secretHash, CancellationToken cancellationToken)
    {
        var r = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(s => s.SecretHash == secretHash, cancellationToken);
        return r is null ? null : Session.Restore(r.Id, r.MemberId, r.SecretHash, r.CreatedAt, r.LastUsedAt);
    }

    public async Task TouchSessionAsync(Guid sessionId, DateTime now, CancellationToken cancellationToken) =>
        await db.Sessions.Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastUsedAt, now), cancellationToken);

    public async Task EndSessionAsync(string secretHash, CancellationToken cancellationToken) =>
        await db.Sessions.Where(s => s.SecretHash == secretHash).ExecuteDeleteAsync(cancellationToken);

    internal void AddCreditAccount(Guid memberId, CreditAccount account, DateTime now)
    {
        var available = Credits.FromDecimal(account.Available);
        db.CreditAccounts.Add(new CreditAccountRecord
        {
            MemberId = memberId,
            Available = available,
            Escrowed = Credits.FromDecimal(account.Escrowed),
        });
        db.LedgerEntries.Add(new LedgerEntryRecord
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Kind = LedgerEntryKind.Grant,
            Amount = available,
            OccurredAt = now,
        });
    }

    private void AddSession(Session session) =>
        db.Sessions.Add(new SessionRecord
        {
            Id = session.Id,
            MemberId = session.MemberId,
            SecretHash = session.SecretHash,
            CreatedAt = session.CreatedAt,
            LastUsedAt = session.LastUsedAt,
        });

    /// <summary>Saves, reporting a lost race (consumed meanwhile, or the address taken) as false instead of throwing.</summary>
    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private static Invitation? ToDomain(InvitationRecord? r) => r is null
        ? null
        : Invitation.Restore(r.Id, EmailAddress.Restore(r.Email), r.IsAdmin, r.SecretHash, r.IssuedAt, r.ExpiresAt, r.ConsumedAt);

    private static Member ToDomain(MemberRecord r) => Member.Restore(
        r.Id, EmailAddress.Restore(r.Email), DisplayName.Restore(r.DisplayName), Language.Restore(r.Language), r.IsAdmin, r.CreatedAt, r.ClosedAt,
        r.Area is { } area && r.Address is { } address ? HomeLocation.Restore(area, address) : null);
}
