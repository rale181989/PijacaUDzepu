using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorEarlyClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EarlyCloseDate",
                table: "Vendors",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EarlyCloseTime",
                table: "Vendors",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EarlyCloseDate",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "EarlyCloseTime",
                table: "Vendors");
        }
    }
}
