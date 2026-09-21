using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantGuarantorConflictOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "applicant_guarantor_conflict_override_approved_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "applicant_guarantor_conflict_override_approved_by",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applicant_guarantor_conflict_override_cnic",
                table: "applications",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applicant_guarantor_conflict_override_reason",
                table: "applications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "applicant_guarantor_conflict_override_approved_at",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "applicant_guarantor_conflict_override_approved_by",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "applicant_guarantor_conflict_override_cnic",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "applicant_guarantor_conflict_override_reason",
                table: "applications");
        }
    }
}
