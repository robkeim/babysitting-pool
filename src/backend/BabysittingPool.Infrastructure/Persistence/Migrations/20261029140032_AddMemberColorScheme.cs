using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabysittingPool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberColorScheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorScheme",
                table: "Members",
                type: "TEXT",
                nullable: false,
                defaultValue: "system");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorScheme",
                table: "Members");
        }
    }
}
