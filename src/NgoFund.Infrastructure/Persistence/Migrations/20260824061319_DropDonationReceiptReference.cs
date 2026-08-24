using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropDonationReceiptReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "receipt_reference",
                table: "donations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "receipt_reference",
                table: "donations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
