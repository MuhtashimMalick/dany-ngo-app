using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleFormIntakeAndEducationHealthDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_requested_amount_positive",
                table: "applications");

            migrationBuilder.AddColumn<string>(
                name: "external_file_reference",
                table: "documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "requested_amount",
                table: "applications",
                type: "numeric(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                table: "applicants",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.CreateTable(
                name: "education_application_details",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    census_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    student_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    wmo_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    student_mobile = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    current_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    previous_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    last_exam_total_marks = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    last_exam_marks_obtained = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    previous_year_attendance_percent = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_attendance_days = table.Column<int>(type: "integer", nullable: true),
                    total_academic_days = table.Column<int>(type: "integer", nullable: true),
                    father_jamaat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mother_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mother_father_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    mother_caste = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mother_jamaat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mother_membership_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    mother_cnic = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    mother_monthly_income = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    mother_mobile = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    mother_profession = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_education_application_details", x => x.application_id);
                    table.CheckConstraint("ck_education_details_academic_days_positive", "total_academic_days IS NULL OR total_academic_days > 0");
                    table.CheckConstraint("ck_education_details_attendance_days_nonneg", "total_attendance_days IS NULL OR total_attendance_days >= 0");
                    table.CheckConstraint("ck_education_details_attendance_le_academic_days", "total_attendance_days IS NULL OR total_academic_days IS NULL OR total_attendance_days <= total_academic_days");
                    table.CheckConstraint("ck_education_details_attendance_pct_range", "previous_year_attendance_percent IS NULL OR (previous_year_attendance_percent >= 0 AND previous_year_attendance_percent <= 100)");
                    table.CheckConstraint("ck_education_details_mother_income_nonneg", "mother_monthly_income IS NULL OR mother_monthly_income >= 0");
                    table.ForeignKey(
                        name: "fk_education_application_details_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "health_application_details",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_age = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_health_application_details", x => x.application_id);
                    table.CheckConstraint("ck_health_details_applicant_age_range", "applicant_age IS NULL OR (applicant_age >= 0 AND applicant_age <= 150)");
                    table.ForeignKey(
                        name: "fk_health_application_details_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documents_external_file_reference",
                table: "documents",
                column: "external_file_reference",
                unique: true,
                filter: "external_file_reference IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_requested_amount_positive",
                table: "applications",
                sql: "requested_amount IS NULL OR requested_amount > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "education_application_details");

            migrationBuilder.DropTable(
                name: "health_application_details");

            migrationBuilder.DropIndex(
                name: "ix_documents_external_file_reference",
                table: "documents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_requested_amount_positive",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "external_file_reference",
                table: "documents");

            migrationBuilder.AlterColumn<decimal>(
                name: "requested_amount",
                table: "applications",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "gender",
                table: "applicants",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_requested_amount_positive",
                table: "applications",
                sql: "requested_amount > 0");
        }
    }
}
