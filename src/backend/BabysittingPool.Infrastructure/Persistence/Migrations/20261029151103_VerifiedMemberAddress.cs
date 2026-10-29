using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabysittingPool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VerifiedMemberAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Area",
                table: "Members",
                newName: "Postcode");

            migrationBuilder.AddColumn<string>(
                name: "Commune",
                table: "Members",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartmentNumber",
                table: "Members",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InseeCode",
                table: "Members",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "Members",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Commune",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "DepartmentNumber",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "InseeCode",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "Members");

            migrationBuilder.RenameColumn(
                name: "Postcode",
                table: "Members",
                newName: "Area");
        }
    }
}
