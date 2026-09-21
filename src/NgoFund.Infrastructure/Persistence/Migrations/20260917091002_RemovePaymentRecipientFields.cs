using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NgoFund.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePaymentRecipientFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "paid_to_cnic",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "paid_to_name",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "paid_to_relation",
                table: "payments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "paid_to_cnic",
                table: "payments",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paid_to_name",
                table: "payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paid_to_relation",
                table: "payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
