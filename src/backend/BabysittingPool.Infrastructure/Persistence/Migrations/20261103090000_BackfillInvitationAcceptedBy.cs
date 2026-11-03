using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BabysittingPool.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data only, no model change. InvitationAcceptedByMember added AcceptedByMemberId without filling it in, so an
    /// Invitation accepted before it kept a null id and Erasure never found it, leaving the erased Member's email in the
    /// Invitations list. This repairs those rows once.
    /// </summary>
    [DbContext(typeof(BabysittingPoolDbContext))]
    [Migration("20261103090000_BackfillInvitationAcceptedBy")]
    public partial class BackfillInvitationAcceptedBy : Migration
    {
        /// <summary>
        /// An accepted Invitation with no id whose email no Member has any more belonged to an erased Member (Erasure
        /// replaced their email), so it gets the same replacement.
        /// </summary>
        public const string TombstoneOrphansSql = """
            UPDATE "Invitations" SET "Email" = 'Former member'
            WHERE "ConsumedAt" IS NOT NULL AND "AcceptedByMemberId" IS NULL
              AND NOT EXISTS (SELECT 1 FROM "Members" m WHERE m."Email" = "Invitations"."Email");
            """;

        /// <summary>
        /// The rest get the Member with that email created closest to when the Invitation was accepted, which tells apart
        /// a Member from a later one who returned on the same address.
        /// </summary>
        public const string LinkToMemberSql = """
            UPDATE "Invitations" SET "AcceptedByMemberId" = (
                SELECT m."Id" FROM "Members" m WHERE m."Email" = "Invitations"."Email"
                ORDER BY ABS(julianday(m."CreatedAt") - julianday("Invitations"."ConsumedAt")) LIMIT 1)
            WHERE "ConsumedAt" IS NOT NULL AND "AcceptedByMemberId" IS NULL AND "Email" <> 'Former member';
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(TombstoneOrphansSql);
            migrationBuilder.Sql(LinkToMemberSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
