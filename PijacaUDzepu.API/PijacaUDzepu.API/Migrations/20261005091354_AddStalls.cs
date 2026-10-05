using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PijacaUDzepu.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StallId",
                table: "Vendors",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stalls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MarketId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stalls_Markets_MarketId",
                        column: x => x.MarketId,
                        principalTable: "Markets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_StallId",
                table: "Vendors",
                column: "StallId");

            migrationBuilder.CreateIndex(
                name: "IX_Stalls_MarketId_Label",
                table: "Stalls",
                columns: new[] { "MarketId", "Label" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Vendors_Stalls_StallId",
                table: "Vendors",
                column: "StallId",
                principalTable: "Stalls",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vendors_Stalls_StallId",
                table: "Vendors");

            migrationBuilder.DropTable(
                name: "Stalls");

            migrationBuilder.DropIndex(
                name: "IX_Vendors_StallId",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "StallId",
                table: "Vendors");
        }
    }
}
