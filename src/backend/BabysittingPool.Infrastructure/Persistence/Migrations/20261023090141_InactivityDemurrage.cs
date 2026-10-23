using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabysittingPool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InactivityDemurrage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Boundary",
                table: "LedgerEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_MemberId_Boundary",
                table: "LedgerEntries",
                columns: new[] { "MemberId", "Boundary" },
                unique: true,
                filter: "Boundary IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_MemberId_Boundary",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "Boundary",
                table: "LedgerEntries");
        }
    }
}
