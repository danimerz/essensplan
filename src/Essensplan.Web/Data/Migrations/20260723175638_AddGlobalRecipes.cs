using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Essensplan.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create the new junction table
            migrationBuilder.CreateTable(
                name: "HouseholdRecipes",
                columns: table => new
                {
                    HouseholdId = table.Column<int>(type: "int", nullable: false),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdRecipes", x => new { x.HouseholdId, x.RecipeId });
                    table.ForeignKey(
                        name: "FK_HouseholdRecipes_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdRecipes_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdRecipes_RecipeId",
                table: "HouseholdRecipes",
                column: "RecipeId");

            // Step 2: Migrate existing data — copy HouseholdId from Recipes into the junction table
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `HouseholdRecipes` (`HouseholdId`, `RecipeId`, `AddedAt`)
                SELECT `HouseholdId`, `Id`, UTC_TIMESTAMP()
                FROM `Recipes`
                WHERE `HouseholdId` > 0");

            // Step 3: Remove old FK, index and column from Recipes
            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Households_HouseholdId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_HouseholdId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Recipes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdRecipes");

            migrationBuilder.AddColumn<int>(
                name: "HouseholdId",
                table: "Recipes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_HouseholdId",
                table: "Recipes",
                column: "HouseholdId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Households_HouseholdId",
                table: "Recipes",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
