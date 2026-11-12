using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCourierOfferedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CourierOfferedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            // Porudžbine za kurira koje je prodavac već potvrdio važe kao ponuđene kuririma (ranije pravilo:
            // ponuda odmah po potvrdi), da ne nestanu sa spiska kurira posle nadogradnje. 1 = Courier, 0 = Pending.
            migrationBuilder.Sql(
                "UPDATE \"Orders\" SET \"CourierOfferedAt\" = COALESCE(\"RespondedAt\", \"CreatedAt\") " +
                "WHERE \"Fulfillment\" = 1 AND \"Status\" <> 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourierOfferedAt",
                table: "Orders");
        }
    }
}
