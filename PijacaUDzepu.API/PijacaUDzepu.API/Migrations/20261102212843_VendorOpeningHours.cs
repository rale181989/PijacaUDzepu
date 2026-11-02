using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using PijacaUDzepu.API.Models;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class VendorOpeningHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<DateOnly>>(
                name: "ClosedDates",
                table: "Vendors",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<List<DeliveryScheduleEntry>>(
                name: "OpeningHours",
                table: "Vendors",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OrdersPaused",
                table: "Vendors",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedDates",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "OpeningHours",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "OrdersPaused",
                table: "Vendors");
        }
    }
}
