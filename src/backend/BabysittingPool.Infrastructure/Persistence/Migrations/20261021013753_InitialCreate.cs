using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabysittingPool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreditAccounts",
                columns: table => new
                {
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AvailableQuarterCredits = table.Column<long>(type: "INTEGER", nullable: false),
                    EscrowedQuarterCredits = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditAccounts", x => x.MemberId);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    AmountQuarterCredits = table.Column<long>(type: "INTEGER", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_CreditAccounts_MemberId",
                        column: x => x.MemberId,
                        principalTable: "CreditAccounts",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_MemberId_OccurredAt_Id",
                table: "LedgerEntries",
                columns: new[] { "MemberId", "OccurredAt", "Id" });

            // Append-only Ledger (ADR 0004): enforced below the app.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER LedgerEntries_no_update BEFORE UPDATE ON LedgerEntries
                BEGIN SELECT RAISE(ABORT, 'LedgerEntries is append-only: UPDATE is not allowed'); END;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER LedgerEntries_no_delete BEFORE DELETE ON LedgerEntries
                BEGIN SELECT RAISE(ABORT, 'LedgerEntries is append-only: DELETE is not allowed'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER LedgerEntries_no_update;");
            migrationBuilder.Sql("DROP TRIGGER LedgerEntries_no_delete;");

            migrationBuilder.DropTable(
                name: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "CreditAccounts");
        }
    }
}
