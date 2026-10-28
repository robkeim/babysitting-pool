using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Domain.Identity;
using BabysittingPool.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Infrastructure;

/// <summary>Creates Members directly, for Development and E2E only; Production only ever uses Invitations.</summary>
public static class MemberSeeder
{
    /// <summary>
    /// Adds a Member whose Credit Account is the sum of <paramref name="grants"/>, with one Grant Ledger entry per
    /// amount, one hour apart starting now, and optionally a profile <paramref name="home"/>. Does nothing if the address already belongs to a Member.
    /// </summary>
    public static async Task<Guid> SeedMemberAsync(
        this IServiceProvider services,
        string email,
        string displayName,
        bool isAdmin,
        IReadOnlyList<Credits> grants,
        HomeLocation? home = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BabysittingPoolDbContext>();
        var existing = await db.Members.SingleOrDefaultAsync(m => m.Email == email && m.ClosedAt == null, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var memberId = Guid.NewGuid();
        db.Members.Add(new MemberRecord
        {
            Id = memberId,
            Email = email,
            DisplayName = displayName,
            IsAdmin = isAdmin,
            CreatedAt = now,
            Area = home?.Area,
            Address = home?.Address,
        });
        db.CreditAccounts.Add(new CreditAccountRecord
        {
            MemberId = memberId,
            Available = Credits.FromQuarterCredits(grants.Sum(g => g.QuarterCredits)),
            Escrowed = Credits.FromQuarterCredits(0),
        });
        for (var i = 0; i < grants.Count; i++)
        {
            db.LedgerEntries.Add(new LedgerEntryRecord
            {
                Id = Guid.NewGuid(),
                MemberId = memberId,
                Kind = LedgerEntryKind.Grant,
                Amount = grants[i],
                OccurredAt = now.AddHours(i),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return memberId;
    }

    /// <summary>
    /// Development: one Admin and three Members with different balances (ADR 0011), then logs the Admin's
    /// sign-in link through the outbox, never delivering it. Safe to run on every start. With a
    /// <paramref name="baseAddress"/> the seed addresses are its plus-addresses (<c>local+user1@domain</c>), so
    /// real email reaches one inbox; otherwise they are <c>example.test</c> addresses.
    /// </summary>
    public static async Task SeedDevelopmentAsync(
        this IServiceProvider services,
        string? adminEmail = null,
        string? baseAddress = null,
        CancellationToken cancellationToken = default)
    {
        string Address(string tag, string fallback) =>
            baseAddress is not null && baseAddress.LastIndexOf('@') is > 0 and var at
                ? $"{baseAddress[..at]}+{tag}{baseAddress[at..]}"
                : fallback;

        adminEmail ??= Address("admin", "admin@example.test");
        var admin = Credits.FromDecimal(CreditAccount.StartingBalance);
        await services.SeedMemberAsync(adminEmail, "Dev Admin", true, [admin], cancellationToken: cancellationToken);
        await services.SeedMemberAsync(Address("user1", "ryan@example.test"), "Ryan (10.0)", false, [admin], cancellationToken: cancellationToken);
        await services.SeedMemberAsync(Address("user2", "sarah@example.test"), "Sarah (0.0)", false, [], cancellationToken: cancellationToken);
        await services.SeedMemberAsync(
            Address("user3", "oscar@example.test"), "Oscar (25.5)", false, [admin, Credits.FromDecimal(15.5m)], cancellationToken: cancellationToken);

        if (EmailAddress.TryParse(adminEmail, out var address))
        {
            await using var scope = services.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<EmailOutbox>();
            var handler = ActivatorUtilities.CreateInstance<RequestSignInLinkHandler>(
                scope.ServiceProvider, new OutboxOnly(outbox));
            await handler.HandleAsync(address, cancellationToken);
        }
    }

    /// <summary>Keeps and logs a message in the outbox without passing it on to a real sender.</summary>
    private sealed class OutboxOnly(EmailOutbox outbox) : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            outbox.SendWithoutDeliveryAsync(message, cancellationToken);
    }
}
