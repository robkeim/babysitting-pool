using backend.tests.Support;
using BabysittingPool.Domain.Credits;
using BabysittingPool.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.tests.Infrastructure;

public class PersistenceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Migrations_apply_to_an_empty_database_and_store_credits_as_integer_quarters()
    {
        var memberId = await SeedAccountWithGrantAsync(Credits.FromDecimal(10.25m));

        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        (await db.CreditAccounts.SingleAsync(a => a.MemberId == memberId)).Available.Value.Should().Be(10.25m);
        await using var connection = new SqliteConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT typeof(AvailableQuarterCredits), AvailableQuarterCredits FROM CreditAccounts";
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("integer");
        reader.GetInt64(1).Should().Be(41);
    }

    [Fact]
    public async Task Ledger_rejects_update()
    {
        await SeedAccountWithGrantAsync(Credits.FromDecimal(10m));
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);

        var act = () => db.Database.ExecuteSqlRawAsync("UPDATE LedgerEntries SET AmountQuarterCredits = 999");

        await act.Should().ThrowAsync<Exception>().WithMessage("*append-only*");
    }

    [Fact]
    public async Task Ledger_rejects_delete()
    {
        await SeedAccountWithGrantAsync(Credits.FromDecimal(10m));
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);

        var act = () => db.Database.ExecuteSqlRawAsync("DELETE FROM LedgerEntries");

        await act.Should().ThrowAsync<Exception>().WithMessage("*append-only*");
    }

    private async Task<Guid> SeedAccountWithGrantAsync(Credits amount)
    {
        var memberId = Guid.NewGuid();
        await using var scope = _database.CreateScope();
        var db = _database.NewContext(scope);
        db.CreditAccounts.Add(new CreditAccountRecord
        {
            MemberId = memberId,
            Available = amount,
            Escrowed = Credits.FromQuarterCredits(0),
        });
        db.LedgerEntries.Add(new LedgerEntryRecord
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            Kind = LedgerEntryKind.Grant,
            Amount = amount,
            OccurredAt = _database.Clock.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync();
        return memberId;
    }
}
