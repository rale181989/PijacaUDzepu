using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNotDelivered : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NotDeliveredAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotDeliveredReason",
                table: "Orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotDeliveredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "NotDeliveredReason",
                table: "Orders");
        }
    }
}
