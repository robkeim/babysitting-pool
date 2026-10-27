using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabysittingPool.Infrastructure.Persistence.RateLimitMigrations;

/// <inheritdoc />
public partial class AddRateLimitCounters : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RateLimitCounters",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", nullable: false),
                WindowEnd = table.Column<long>(type: "INTEGER", nullable: false),
                Count = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RateLimitCounters", x => x.Key);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RateLimitCounters_WindowEnd",
            table: "RateLimitCounters",
            column: "WindowEnd");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RateLimitCounters");
    }
}
