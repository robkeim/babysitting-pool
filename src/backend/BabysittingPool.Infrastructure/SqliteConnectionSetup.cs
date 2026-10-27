using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BabysittingPool.Infrastructure;

/// <summary>Applies the per-connection PRAGMAs of ADR 0004 so tests and production behave identically.</summary>
internal sealed class SqliteConnectionSetup : DbConnectionInterceptor
{
    private const string Pragmas = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        // nosemgrep: csharp.lang.security.sqli.csharp-sqli.csharp-sqli, project.csharp.raw-sql-outside-migrations -- Pragmas is a compile-time constant
        command.CommandText = Pragmas;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        // nosemgrep: csharp.lang.security.sqli.csharp-sqli.csharp-sqli, project.csharp.raw-sql-outside-migrations -- Pragmas is a compile-time constant
        command.CommandText = Pragmas;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
