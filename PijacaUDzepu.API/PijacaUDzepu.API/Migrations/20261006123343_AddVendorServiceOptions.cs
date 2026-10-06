using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorServiceOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsReservations",
                table: "Vendors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DeliversDaily",
                table: "Vendors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryDays",
                table: "Vendors",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryRadiusKm",
                table: "Vendors",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinOrderAmount",
                table: "Vendors",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OffersDelivery",
                table: "Vendors",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptsReservations",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "DeliversDaily",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "DeliveryDays",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "DeliveryRadiusKm",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "MinOrderAmount",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "OffersDelivery",
                table: "Vendors");
        }
    }
}
