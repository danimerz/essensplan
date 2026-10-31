using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Essensplan.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hinweis: Der Modell-Schnappschuss enthielt die Spalten ImageUrl/ProductCategory/Price/IsPromotion
            // der Einkaufsliste noch nicht (von Hand geschriebene Migrationen vom Juli). Sie existieren bereits
            // → hier nur die neue Tabelle.
            migrationBuilder.CreateTable(
                name: "ShoppingListApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    HouseholdId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KeyHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KeyPrefix = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingListApiKeys_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListApiKeys_HouseholdId",
                table: "ShoppingListApiKeys",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListApiKeys_KeyHash",
                table: "ShoppingListApiKeys",
                column: "KeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShoppingListApiKeys");
        }
    }
}
