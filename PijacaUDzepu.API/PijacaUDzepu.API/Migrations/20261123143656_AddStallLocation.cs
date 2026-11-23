using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStallLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "StallLatitude",
                table: "Vendors",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StallLocationAt",
                table: "Vendors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "StallLongitude",
                table: "Vendors",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Markets",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Markets",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StallLatitude",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "StallLocationAt",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "StallLongitude",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Markets");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Markets");
        }
    }
}
