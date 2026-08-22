using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loan_agreements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fund_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    installment_count = table.Column<int>(type: "integer", nullable: false),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    first_due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    written_off_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    written_off_by = table.Column<Guid>(type: "uuid", nullable: true),
                    written_off_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loan_agreements", x => x.id);
                    table.CheckConstraint("ck_loan_agreements_principal_positive", "principal_amount > 0");
                    table.ForeignKey(
                        name: "fk_loan_agreements_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loan_agreements_fund_categories_fund_category_id",
                        column: x => x.fund_category_id,
                        principalTable: "fund_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loan_installments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_agreement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    amount_due = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loan_installments", x => x.id);
                    table.CheckConstraint("ck_loan_installments_amount_positive", "amount_due > 0");
                    table.CheckConstraint("ck_loan_installments_sequence_positive", "sequence_no > 0");
                    table.ForeignKey(
                        name: "fk_loan_installments_loan_agreements_loan_agreement_id",
                        column: x => x.loan_agreement_id,
                        principalTable: "loan_agreements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loan_repayments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    repayment_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    loan_agreement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    repayment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    instrument_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bank_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    received_from_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    received_from_cnic = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loan_repayments", x => x.id);
                    table.CheckConstraint("ck_loan_repayments_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_loan_repayments_loan_agreements_loan_agreement_id",
                        column: x => x.loan_agreement_id,
                        principalTable: "loan_agreements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "id", "created_at", "created_by", "data_type", "description", "is_editable", "key", "updated_at", "updated_by", "value" },
                values: new object[,]
                {
                    { new Guid("1c6e48d5-cb0a-be34-7fb3-bbe50e3c40e9"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "number", "Default number of installments suggested when authoring a new loan agreement.", true, "loan.default_installment_count", null, null, "10" },
                    { new Guid("c62e575b-2d59-b2e2-da9a-37992c96df9e"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "number", "Upper bound on the installment count a loan agreement may be created with.", true, "loan.max_installment_count", null, null, "60" }
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "created_by", "description", "display_name", "module" },
                values: new object[,]
                {
                    { new Guid("548b3971-b4da-0e85-b75f-307e14ffc716"), "loans.writeoff", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Write Off Loans", "loans" },
                    { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), "loans.manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Create/Cancel Loan Agreements", "loans" },
                    { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), "loans.repay", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Record Loan Repayments", "loans" },
                    { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), "loans.void", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Void Loan Repayments", "loans" },
                    { new Guid("eb6df8d0-5450-9416-53f6-c4a3cab94aca"), "loans.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "View Loans", "loans" }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("eb6df8d0-5450-9416-53f6-c4a3cab94aca"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") },
                    { new Guid("eb6df8d0-5450-9416-53f6-c4a3cab94aca"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") },
                    { new Guid("548b3971-b4da-0e85-b75f-307e14ffc716"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("eb6df8d0-5450-9416-53f6-c4a3cab94aca"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") },
                    { new Guid("548b3971-b4da-0e85-b75f-307e14ffc716"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") },
                    { new Guid("eb6df8d0-5450-9416-53f6-c4a3cab94aca"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_loan_agreements_application_id",
                table: "loan_agreements",
                column: "application_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_loan_agreements_fund_category_id",
                table: "loan_agreements",
                column: "fund_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_agreements_loan_number",
                table: "loan_agreements",
                column: "loan_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loan_installments_loan_agreement_id_sequence_no",
                table: "loan_installments",
                columns: new[] { "loan_agreement_id", "sequence_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loan_repayments_loan_agreement_id",
                table: "loan_repayments",
                column: "loan_agreement_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_repayments_repayment_number",
                table: "loan_repayments",
                column: "repayment_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_installments");

            migrationBuilder.DropTable(
                name: "loan_repayments");

            migrationBuilder.DropTable(
                name: "loan_agreements");

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: new Guid("1c6e48d5-cb0a-be34-7fb3-bbe50e3c40e9"));

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "id",
                keyValue: new Guid("c62e575b-2d59-b2e2-da9a-37992c96df9e"));

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("eb6df8d0-5450-9416-53f6-c4a3cab94aca"), new Guid("1bcca810-9298-8bb5-862d-b870025e4704") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("eb6df8d0-5450-9416-53f6-c4a3cab94aca"), new Guid("2385a4ab-ec7f-1fae-56c8-bfe26ec375f4") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("548b3971-b4da-0e85-b75f-307e14ffc716"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("eb6df8d0-5450-9416-53f6-c4a3cab94aca"), new Guid("6ca87666-f14f-8970-12e3-d3193bcb01ad") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("548b3971-b4da-0e85-b75f-307e14ffc716"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("eb6df8d0-5450-9416-53f6-c4a3cab94aca"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("548b3971-b4da-0e85-b75f-307e14ffc716"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("9340c2a7-afa8-82f9-4690-2c708d41d32f"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("a40bf9c9-b39a-f7db-ef12-066357e36c2f"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("ba427a6d-d489-d784-f380-a865bcf112fa"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("eb6df8d0-5450-9416-53f6-c4a3cab94aca"));
        }
    }
}
