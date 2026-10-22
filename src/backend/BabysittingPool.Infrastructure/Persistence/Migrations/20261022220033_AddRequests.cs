using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabysittingPool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "LedgerEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequesterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SitterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ChildCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    Transport = table.Column<string>(type: "TEXT", nullable: false),
                    Meals = table.Column<int>(type: "INTEGER", nullable: false),
                    Area = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    CostQuarterCredits = table.Column<long>(type: "INTEGER", nullable: false),
                    ClaimCutoffAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requests_Members_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Requests_Members_SitterId",
                        column: x => x.SitterId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_RequestId_Kind",
                table: "LedgerEntries",
                columns: new[] { "RequestId", "Kind" },
                unique: true,
                filter: "RequestId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RequesterId_PostedAt_Id",
                table: "Requests",
                columns: new[] { "RequesterId", "PostedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_SitterId",
                table: "Requests",
                column: "SitterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_RequestId_Kind",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "LedgerEntries");
        }
    }
}
