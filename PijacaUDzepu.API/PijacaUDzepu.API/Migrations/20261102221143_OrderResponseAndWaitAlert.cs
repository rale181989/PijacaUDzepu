using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class OrderResponseAndWaitAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WaitAlertSentAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            // Stare porudžbine se ne prijavljuju kao "čeka predugo" - upozorenja samo za porudžbine posle ove migracije
            migrationBuilder.Sql("UPDATE \"Orders\" SET \"WaitAlertSentAt\" = NOW()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "WaitAlertSentAt",
                table: "Orders");
        }
    }
}
