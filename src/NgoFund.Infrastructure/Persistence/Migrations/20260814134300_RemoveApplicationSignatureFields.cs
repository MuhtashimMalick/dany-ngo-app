using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveApplicationSignatureFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_applications_documents_signature_document_id",
                table: "applications");

            migrationBuilder.DropIndex(
                name: "ix_applications_signature_document_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "signature_document_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "signed_by_name",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "signed_by_name",
                table: "application_guarantors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "signature_document_id",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signed_by_name",
                table: "applications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signed_by_name",
                table: "application_guarantors",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_signature_document_id",
                table: "applications",
                column: "signature_document_id");

            migrationBuilder.AddForeignKey(
                name: "fk_applications_documents_signature_document_id",
                table: "applications",
                column: "signature_document_id",
                principalTable: "documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
