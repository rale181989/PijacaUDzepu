using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderClosedByAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedByAdminAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosedByAdminId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClosedByAdminId",
                table: "Orders",
                column: "ClosedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_ClosedByAdminId",
                table: "Orders",
                column: "ClosedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_ClosedByAdminId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ClosedByAdminId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ClosedByAdminAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ClosedByAdminId",
                table: "Orders");
        }
    }
}
