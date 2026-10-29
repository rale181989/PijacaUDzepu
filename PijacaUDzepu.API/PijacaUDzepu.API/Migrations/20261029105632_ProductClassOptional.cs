using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class ProductClassOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ProductClass",
                table: "Products",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // Proizvodi koji nisu menjani od uvođenja klase dobili su "I klasa" samo kao podrazumevanu vrednost
            migrationBuilder.Sql(
                "UPDATE \"Products\" SET \"ProductClass\" = NULL " +
                "WHERE \"UpdatedAt\" < TIMESTAMPTZ '2026-10-29 10:15:27+00';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Products\" SET \"ProductClass\" = 0 WHERE \"ProductClass\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "ProductClass",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
