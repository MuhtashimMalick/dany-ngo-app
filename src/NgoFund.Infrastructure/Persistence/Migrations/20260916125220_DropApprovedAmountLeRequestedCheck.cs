using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropApprovedAmountLeRequestedCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_approved_amount_le_requested",
                table: "applications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-adding this constraint can fail once a real approval has legitimately exceeded
            // its requested amount (v1.8 correction) — that is expected/acceptable, not a bug.
            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_approved_amount_le_requested",
                table: "applications",
                sql: "approved_amount IS NULL OR approved_amount <= requested_amount");
        }
    }
}
