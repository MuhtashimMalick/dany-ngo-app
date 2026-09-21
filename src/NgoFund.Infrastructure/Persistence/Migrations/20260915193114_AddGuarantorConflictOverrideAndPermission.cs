using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuarantorConflictOverrideAndPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "conflict_override_approved_at",
                table: "application_guarantors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "conflict_override_approved_by",
                table: "application_guarantors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "conflict_override_reason",
                table: "application_guarantors",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "created_at", "created_by", "description", "display_name", "module" },
                values: new object[] { new Guid("ef8f3f78-687a-2f21-c3d1-8facd81d7a1a"), "applications.overrideguarantor", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, "Override Guarantor Conflicts", "applications" });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[] { new Guid("ef8f3f78-687a-2f21-c3d1-8facd81d7a1a"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ef8f3f78-687a-2f21-c3d1-8facd81d7a1a"), new Guid("e9da1089-6ad9-7f34-3937-7976d95ae7a0") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("ef8f3f78-687a-2f21-c3d1-8facd81d7a1a"));

            migrationBuilder.DropColumn(
                name: "conflict_override_approved_at",
                table: "application_guarantors");

            migrationBuilder.DropColumn(
                name: "conflict_override_approved_by",
                table: "application_guarantors");

            migrationBuilder.DropColumn(
                name: "conflict_override_reason",
                table: "application_guarantors");
        }
    }
}
