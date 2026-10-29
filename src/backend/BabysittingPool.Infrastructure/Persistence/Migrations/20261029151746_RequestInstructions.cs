using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabysittingPool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "Requests",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "Requests");
        }
    }
}
