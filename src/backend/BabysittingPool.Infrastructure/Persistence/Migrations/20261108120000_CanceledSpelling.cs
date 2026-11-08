using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabysittingPool.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data only, no model change. The Request status <c>Cancelled</c> and the Notification kind <c>request_cancelled</c>
    /// became <c>Canceled</c> and <c>request_canceled</c> (en-US spelling, #246); both are stored as text.
    /// </summary>
    [DbContext(typeof(BabysittingPoolDbContext))]
    [Migration("20261108120000_CanceledSpelling")]
    public partial class CanceledSpelling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Requests" SET "Status" = 'Canceled' WHERE "Status" = 'Cancelled';""");
            migrationBuilder.Sql("""UPDATE "Notifications" SET "Kind" = 'request_canceled' WHERE "Kind" = 'request_cancelled';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Requests" SET "Status" = 'Cancelled' WHERE "Status" = 'Canceled';""");
            migrationBuilder.Sql("""UPDATE "Notifications" SET "Kind" = 'request_cancelled' WHERE "Kind" = 'request_canceled';""");
        }
    }
}
