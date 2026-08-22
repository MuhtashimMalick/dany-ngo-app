using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSlotKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documents_application_guarantor_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_documents_application_id",
                table: "documents");

            migrationBuilder.AddColumn<string>(
                name: "slot_key",
                table: "documents",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_application_guarantor_id_slot_key",
                table: "documents",
                columns: new[] { "application_guarantor_id", "slot_key" });

            migrationBuilder.CreateIndex(
                name: "ix_documents_application_id_slot_key",
                table: "documents",
                columns: new[] { "application_id", "slot_key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documents_application_guarantor_id_slot_key",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "ix_documents_application_id_slot_key",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "slot_key",
                table: "documents");

            migrationBuilder.CreateIndex(
                name: "ix_documents_application_guarantor_id",
                table: "documents",
                column: "application_guarantor_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_application_id",
                table: "documents",
                column: "application_id");
        }
    }
}
