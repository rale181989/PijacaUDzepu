using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using PijacaUDzepu.API.Models;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVendorDeliverySchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliversDaily",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "DeliveryDays",
                table: "Vendors");

            migrationBuilder.AddColumn<List<DeliveryScheduleEntry>>(
                name: "DeliverySchedule",
                table: "Vendors",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliverySchedule",
                table: "Vendors");

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
        }
    }
}
