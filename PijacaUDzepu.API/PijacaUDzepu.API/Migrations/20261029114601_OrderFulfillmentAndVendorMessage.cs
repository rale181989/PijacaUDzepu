using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class OrderFulfillmentAndVendorMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "Orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Fulfillment",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VendorMessage",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VendorMessageAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            // Dosadašnje porudžbine sa adresom napravljene su za dostavu kurirom (Fulfillment = Courier)
            migrationBuilder.Sql("UPDATE \"Orders\" SET \"Fulfillment\" = 1 WHERE \"DeliveryAddress\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Fulfillment",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VendorMessage",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VendorMessageAt",
                table: "Orders");
        }
    }
}
