using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabysittingPool.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotificationsFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The flag is retired (ADR 0016, #160): its stored settings go with its registry entry.
            migrationBuilder.Sql("DELETE FROM \"FeatureFlags\" WHERE \"Name\" = 'notifications';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
