using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class VendorMultiplePhones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Phones",
                table: "Vendors",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            // Postojeći broj prelazi u listu da se ne bi izgubio
            migrationBuilder.Sql(
                "UPDATE \"Vendors\" SET \"Phones\" = ARRAY[trim(\"Phone\")] " +
                "WHERE \"Phone\" IS NOT NULL AND trim(\"Phone\") <> '';");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Vendors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Vendors",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Vendors\" SET \"Phone\" = \"Phones\"[1];");

            migrationBuilder.DropColumn(
                name: "Phones",
                table: "Vendors");
        }
    }
}
