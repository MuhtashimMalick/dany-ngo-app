using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityNarrationToAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "entity_label",
                table: "audit_logs",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entity_number",
                table: "audit_logs",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "audit_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verb",
                table: "audit_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_activity",
                table: "audit_logs",
                column: "occurred_at",
                descending: new bool[0],
                filter: "summary IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_logs_verb",
                table: "audit_logs",
                sql: "verb IS NULL OR verb IN ('Created','Updated','Deleted','Recorded','Voided','StatusChanged','Cancelled','WrittenOff','Deactivated','Reactivated','Blacklisted','PermissionGranted','PermissionRevoked')");

            // pg_trgm is already enabled (see AppDbContext's HasPostgresExtension call) — EF's
            // fluent API can't express a gin_trgm_ops operator class, so this one index is raw SQL.
            // Powers the activity feed's free-text search over Summary (IAuditLogService.Search).
            migrationBuilder.Sql(
                "CREATE INDEX ix_audit_logs_summary_trgm ON audit_logs USING gin (summary gin_trgm_ops) WHERE summary IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_audit_logs_summary_trgm;");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_activity",
                table: "audit_logs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_logs_verb",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "entity_label",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "entity_number",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "summary",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "verb",
                table: "audit_logs");
        }
    }
}
